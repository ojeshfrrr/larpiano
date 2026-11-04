"""Read-only public catalogue and static website for the Portways deployment."""

from __future__ import annotations

import hashlib
import json
import logging
import math
import os
from pathlib import Path
import re
import threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parent
WEB_ROOT = Path(os.environ.get("WEB_ROOT", ROOT.parent / "website")).resolve()
SEED_ROOT = ROOT / "seed"
MAX_MIDI_BYTES = 10 * 1024 * 1024
MAX_MANIFEST_BYTES = 1024 * 1024
MAX_ENTRIES = 1000
DIGEST = re.compile(r"^[0-9a-f]{64}$")
FIELDS = ("sha256", "title", "artist", "source", "license", "profile", "octave",
          "keptNotes", "excludedNotes", "notesAdjusted", "attribution",
          "durationSeconds", "modification")
REQUIRED = set(FIELDS[:9])
DB_LOCK = threading.Lock()
DB_SLOTS = threading.BoundedSemaphore(6)
initialized = False
seed_cache = None
log = logging.getLogger("remapped_catalog")


def load_seeds(seed_root: Path = SEED_ROOT) -> list[tuple[str, dict, bytes]]:
    manifest = seed_root / "manifest.json"
    if not manifest.is_file():
        raise ValueError("Seed manifest is missing")
    if manifest.stat().st_size > MAX_MANIFEST_BYTES:
        raise ValueError("Seed manifest exceeds the size limit")
    entries = json.loads(manifest.read_text(encoding="utf-8"))
    if not isinstance(entries, list) or len(entries) > MAX_ENTRIES:
        raise ValueError("Seed manifest must be a bounded array")
    result = []
    seen = set()
    for entry in entries:
        if not isinstance(entry, dict) or not REQUIRED.issubset(entry):
            raise ValueError("Invalid seed metadata")
        digest = entry["sha256"]
        if not isinstance(digest, str) or not DIGEST.fullmatch(digest) or digest in seen:
            raise ValueError("Invalid or duplicate seed checksum")
        seen.add(digest)
        for field in ("title", "artist", "source", "license", "profile"):
            if not isinstance(entry[field], str) or not 0 < len(entry[field]) <= 2048:
                raise ValueError("Invalid seed text field: " + field)
        source = urlsplit(entry["source"])
        if source.scheme != "https" or not source.hostname or source.username or source.password:
            raise ValueError("Seed source must be an HTTPS URL")
        for field in ("octave", "keptNotes", "excludedNotes"):
            if type(entry[field]) is not int or not (-2 <= entry[field] <= 2 if field == "octave" else 0 <= entry[field] <= 2_000_000):
                raise ValueError("Invalid seed number: " + field)
        if "notesAdjusted" in entry and (type(entry["notesAdjusted"]) is not int or not 0 <= entry["notesAdjusted"] <= 2_000_000):
            raise ValueError("Invalid seed note adjustment count")
        if "durationSeconds" in entry and (type(entry["durationSeconds"]) not in (int, float) or not math.isfinite(entry["durationSeconds"]) or not 0 <= entry["durationSeconds"] <= 86400):
            raise ValueError("Invalid seed duration")
        for field in ("attribution", "modification"):
            if field in entry and (not isinstance(entry[field], str) or len(entry[field]) > 4096):
                raise ValueError("Invalid seed text field: " + field)
        path = seed_root / (digest + ".mid")
        if not path.is_file() or path.stat().st_size > MAX_MIDI_BYTES:
            raise ValueError("Seed MIDI missing or exceeds size limit")
        midi = path.read_bytes()
        if len(midi) < 14 or midi[:4] != b"MThd" or midi[4:8] != b"\x00\x00\x00\x06":
            raise ValueError("Seed is not a standard MIDI file")
        if hashlib.sha256(midi).hexdigest() != digest:
            raise ValueError("Seed checksum mismatch")
        metadata = {field: entry[field] for field in FIELDS if field in entry}
        result.append((digest, metadata, midi))
    return result


def connect():
    import psycopg
    url = os.environ.get("DATABASE_URL")
    if not url:
        raise RuntimeError("Catalogue database is not configured")
    return psycopg.connect(url, connect_timeout=5)


def initialize():
    global initialized
    if initialized:
        return
    with DB_LOCK:
        if initialized:
            return
        from psycopg.types.json import Jsonb
        seeds = load_seeds()
        with DB_SLOTS, connect() as conn:
            with conn.cursor() as cur:
                cur.execute("""CREATE TABLE IF NOT EXISTS remapped_catalog_songs (
                    sha256 char(64) PRIMARY KEY,
                    metadata jsonb NOT NULL,
                    midi bytea NOT NULL CHECK (octet_length(midi) BETWEEN 14 AND 10485760)
                )""")
                for digest, metadata, midi in seeds:
                    cur.execute("""INSERT INTO remapped_catalog_songs (sha256,metadata,midi)
                        VALUES (%s,%s,%s) ON CONFLICT (sha256) DO UPDATE
                        SET metadata=EXCLUDED.metadata,midi=EXCLUDED.midi""",
                        (digest, Jsonb(metadata), midi))
        initialized = True


def static_catalog():
    global seed_cache
    if seed_cache is None:
        with DB_LOCK:
            if seed_cache is None:
                seed_cache = load_seeds()
    return seed_cache


def list_songs():
    if not os.environ.get("DATABASE_URL"):
        return [metadata for _, metadata, _ in static_catalog()]
    initialize()
    with DB_SLOTS, connect() as conn:
        with conn.cursor() as cur:
            cur.execute("SELECT metadata FROM remapped_catalog_songs ORDER BY metadata->>'title', sha256 LIMIT %s", (MAX_ENTRIES,))
            return [row[0] for row in cur.fetchall()]


def get_song(digest: str):
    if not os.environ.get("DATABASE_URL"):
        return next((midi for key, _, midi in static_catalog() if key == digest), None)
    initialize()
    with DB_SLOTS, connect() as conn:
        with conn.cursor() as cur:
            cur.execute("SELECT midi FROM remapped_catalog_songs WHERE sha256=%s", (digest,))
            row = cur.fetchone()
    if row is None:
        return None
    data = bytes(row[0])
    if len(data) > MAX_MIDI_BYTES or hashlib.sha256(data).hexdigest() != digest or data[:4] != b"MThd":
        raise ValueError("Stored MIDI failed integrity verification")
    return data


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        args[0].settimeout(15)
        super().__init__(*args, directory=str(WEB_ROOT), **kwargs)

    def log_message(self, fmt, *args):
        log.info("%s %s", self.address_string(), fmt % args)

    def send_json(self, status: int, payload, head: bool = False):
        content = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if len(content) > 4 * 1024 * 1024:
            status, content = 503, b'{"error":"Catalogue temporarily unavailable"}'
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        if not head:
            self.wfile.write(content)

    def serve_api(self, head=False):
        path = urlsplit(self.path).path
        if path == "/api/remapped/songs":
            try:
                self.send_json(200, list_songs(), head)
            except Exception as exc:
                log.error("Catalogue unavailable (%s)", type(exc).__name__)
                self.send_json(503, {"error": "Catalogue temporarily unavailable"}, head)
            return True
        prefix = "/api/remapped/songs/"
        if path.startswith(prefix):
            match = re.fullmatch(r"([0-9a-f]{64})/midi", path[len(prefix):])
            if not match:
                self.send_json(404, {"error": "Not found"}, head)
                return True
            try:
                data = get_song(match.group(1))
                if data is None:
                    self.send_json(404, {"error": "Not found"}, head)
                    return True
                self.send_response(200)
                self.send_header("Content-Type", "audio/midi")
                self.send_header("Content-Length", str(len(data)))
                self.send_header("X-Content-Type-Options", "nosniff")
                self.send_header("Content-Disposition", 'attachment; filename="' + match.group(1) + '.mid"')
                self.send_header("Cache-Control", "public, max-age=86400, immutable")
                self.end_headers()
                if not head:
                    self.wfile.write(data)
            except Exception as exc:
                log.error("MIDI unavailable (%s)", type(exc).__name__)
                self.send_json(503, {"error": "Song temporarily unavailable"}, head)
            return True
        if path.startswith("/api/"):
            self.send_json(404, {"error": "Not found"}, head)
            return True
        return False

    def safe_static(self):
        path = unquote(urlsplit(self.path).path)
        if "\x00" in path or "\\" in path:
            return False
        parts = path.split("/")
        if any(part in ("..", ".") or part.startswith(".") for part in parts if part):
            return False
        return (WEB_ROOT / path.lstrip("/")).resolve().is_relative_to(WEB_ROOT)

    def do_GET(self):
        if self.serve_api():
            return
        if not self.safe_static():
            self.send_error(404)
            return
        super().do_GET()

    def do_HEAD(self):
        if self.serve_api(head=True):
            return
        if not self.safe_static():
            self.send_error(404)
            return
        super().do_HEAD()

    def list_directory(self, path):
        self.send_error(404)
        return None


class BoundedServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.request_slots = threading.BoundedSemaphore(32)

    def process_request(self, request, client_address):
        if not self.request_slots.acquire(blocking=False):
            try:
                request.sendall(b"HTTP/1.0 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n")
            except OSError:
                pass
            self.shutdown_request(request)
            return
        try:
            super().process_request(request, client_address)
        except BaseException:
            self.request_slots.release()
            raise

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self.request_slots.release()


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    port = int(os.environ.get("PORT", "80"))
    BoundedServer(("0.0.0.0", port), Handler).serve_forever()
