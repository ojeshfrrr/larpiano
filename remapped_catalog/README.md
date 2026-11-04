# Remapped song catalogue

This read-only Python service serves the existing `website` directory and a file-backed catalogue on the existing free Portways project. It has no public write or upload endpoint. If `DATABASE_URL` is set, the service instead uses PostgreSQL; the website remains available if the database is offline, while the catalogue returns HTTP 503.

Build from the repository root with `docker build -f remapped_catalog/Dockerfile -t larpiano-catalog .`. The Docker build context must include `website/` and `remapped_catalog/`; update the root `.dockerignore` before building. On Portways, link a PostgreSQL add-on to the project and redeploy to inject its project-scoped `DATABASE_URL`. The server listens on `PORT`, defaulting to 80.

On its first catalogue request, the server validates `seed/manifest.json` and each SHA-256-named MIDI file. In PostgreSQL mode it creates its table and idempotently inserts or updates entries in one database transaction. A failed validation or database operation leaves the catalogue unavailable instead of publishing partial seed data. The database also supports owner-managed records; ensure licensing and integrity before changing records. Seeds removed from the manifest are not automatically deleted from the database.

- `GET /api/remapped/songs`: JSON array of metadata.
- `GET /api/remapped/songs/{sha256}/midi`: MIDI bytes after server-side checksum validation.

No credentials are returned by the API. MIDI files remain subject to the rights and attribution recorded in the manifest. Each user must verify their own target piano layout; the catalogue does not mark their profile verified.

Run the offline tests with `python -m unittest discover -s remapped_catalog -p test_server.py`. Install `remapped_catalog/requirements.txt` to run the server itself.
