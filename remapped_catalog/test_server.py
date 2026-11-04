import hashlib
import io
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import server


class SeedTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.data = b"MThd\x00\x00\x00\x06\x00\x00\x00\x01\x01\xe0"
        self.digest = hashlib.sha256(self.data).hexdigest()
        self.entry = {"sha256": self.digest, "title": "Example", "artist": "Composer",
                      "source": "https://example.org/score", "license": "Public Domain",
                      "profile": "Calibration required", "octave": 0,
                      "keptNotes": 1, "excludedNotes": 0}
        (self.root / (self.digest + ".mid")).write_bytes(self.data)
        self.write_manifest([self.entry])

    def tearDown(self):
        self.temp.cleanup()

    def write_manifest(self, value):
        (self.root / "manifest.json").write_text(json.dumps(value), encoding="utf-8")

    def test_seed_accepts_matching_header_and_digest(self):
        rows = server.load_seeds(self.root)
        self.assertEqual(rows[0][0], self.digest)
        self.assertEqual(rows[0][2], self.data)

    def test_corrupt_file_rejected(self):
        (self.root / (self.digest + ".mid")).write_bytes(self.data + b"bad")
        with self.assertRaisesRegex(ValueError, "checksum"):
            server.load_seeds(self.root)

    def test_wrong_header_rejected_even_with_matching_hash(self):
        data = b"NOTM" + self.data[4:]
        digest = hashlib.sha256(data).hexdigest()
        self.entry["sha256"] = digest
        (self.root / (digest + ".mid")).write_bytes(data)
        self.write_manifest([self.entry])
        with self.assertRaisesRegex(ValueError, "MIDI"):
            server.load_seeds(self.root)

    def test_duplicate_and_unsafe_hashes_rejected(self):
        self.write_manifest([self.entry, self.entry])
        with self.assertRaises(ValueError):
            server.load_seeds(self.root)
        self.entry["sha256"] = "../outside"
        self.write_manifest([self.entry])
        with self.assertRaises(ValueError):
            server.load_seeds(self.root)

    def test_invalid_optional_values_rejected(self):
        self.entry["durationSeconds"] = -1
        self.write_manifest([self.entry])
        with self.assertRaises(ValueError):
            server.load_seeds(self.root)


class HandlerTests(unittest.TestCase):
    def handler(self, path):
        h = server.Handler.__new__(server.Handler)
        h.path = path
        h.wfile = io.BytesIO()
        h.responses = []
        h.send_response = lambda code: h.responses.append(("status", code))
        h.send_header = lambda key, value: h.responses.append((key, value))
        h.end_headers = lambda: None
        return h

    def test_catalog_returns_array(self):
        h = self.handler("/api/remapped/songs")
        with patch.object(server, "list_songs", return_value=[{"title": "Example"}]):
            self.assertTrue(h.serve_api())
        self.assertEqual(json.loads(h.wfile.getvalue()), [{"title": "Example"}])
        self.assertIn(("status", 200), h.responses)

    def test_database_failure_is_generic_and_head_has_no_body(self):
        h = self.handler("/api/remapped/songs")
        with patch.object(server, "list_songs", side_effect=RuntimeError("secret")):
            self.assertTrue(h.serve_api(head=True))
        self.assertIn(("status", 503), h.responses)
        self.assertEqual(h.wfile.getvalue(), b"")

    def test_download_only_accepts_digest_paths(self):
        h = self.handler("/api/remapped/songs/not-a-hash/midi")
        with patch.object(server, "get_song") as get:
            self.assertTrue(h.serve_api())
        get.assert_not_called()
        self.assertIn(("status", 404), h.responses)

    def test_download_returns_bytes_and_immutable_header(self):
        digest = "a" * 64
        h = self.handler("/api/remapped/songs/" + digest + "/midi")
        with patch.object(server, "get_song", return_value=b"MThd\x00"):
            self.assertTrue(h.serve_api())
        self.assertEqual(h.wfile.getvalue(), b"MThd\x00")
        self.assertIn(("Content-Type", "audio/midi"), h.responses)
        self.assertIn(("status", 200), h.responses)

    def test_static_path_rejects_traversal(self):
        for path in ("/%2e%2e/secrets", "/.env", "/foo%5cbar"):
            self.assertFalse(self.handler(path).safe_static())
        self.assertTrue(self.handler("/assets/favicon.svg").safe_static())


class StaticCatalogTests(unittest.TestCase):
    def test_catalog_and_midi_work_without_database_credentials(self):
        with patch.dict(os.environ, {}, clear=True), patch.object(server, "seed_cache", None):
            songs = server.list_songs()
            self.assertEqual(len(songs), 12)
            for entry in songs:
                midi = server.get_song(entry["sha256"])
                self.assertEqual(hashlib.sha256(midi).hexdigest(), entry["sha256"])
            self.assertIsNone(server.get_song("a" * 64))


class DatabaseReadTests(unittest.TestCase):
    class Connection:
        def __init__(self, row):
            self.row = row
            self.query = None

        def __enter__(self): return self
        def __exit__(self, *args): pass
        def cursor(self): return self
        def execute(self, query, params): self.query = (query, params)
        def fetchone(self): return self.row

    def test_stored_midi_checksum_is_rechecked(self):
        data = b"MThd\x00\x00\x00\x06\x00\x00\x00\x01\x01\xe0"
        digest = hashlib.sha256(data).hexdigest()
        conn = self.Connection((data,))
        with patch.dict(os.environ, {"DATABASE_URL": "postgres://test"}), patch.object(server, "initialize"), patch.object(server, "connect", return_value=conn):
            self.assertEqual(server.get_song(digest), data)
            self.assertEqual(conn.query[1], (digest,))
            conn.row = (data + b"corruption",)
            with self.assertRaisesRegex(ValueError, "integrity"):
                server.get_song(digest)

    def test_missing_song_returns_none(self):
        with patch.dict(os.environ, {"DATABASE_URL": "postgres://test"}), patch.object(server, "initialize"), patch.object(server, "connect", return_value=self.Connection(None)):
            self.assertIsNone(server.get_song("a" * 64))


if __name__ == "__main__":
    unittest.main()
