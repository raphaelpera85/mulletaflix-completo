from __future__ import annotations

from tools import strm_downloader as downloader


class FilesCollection:
    def __init__(self, existing=None, *, fail_find=False, fail_scan=False):
        self.existing = existing
        self.fail_find = fail_find
        self.fail_scan = fail_scan
        self.find_calls = []
        self.update_calls = []
        self.insert_calls = []

    def find_one(self, query, projection):
        self.find_calls.append((query, projection))
        if self.fail_find:
            raise ConnectionError("Mongo unavailable")
        if "local_path" in query:
            return self.existing
        return None

    def find(self, query, projection):
        if self.fail_scan:
            raise ConnectionError("Mongo scan unavailable")
        return iter([])

    def update_one(self, query, update, **kwargs):
        self.update_calls.append((query, update, kwargs))

    def insert_one(self, document):
        self.insert_calls.append(document)


class MongoClientDouble:
    def __init__(self, files):
        self.files = files
        self.closed = False
        self.uri = None
        self.options = None

    def __getitem__(self, database):
        assert database == "nebula-test"
        return type("Database", (), {"files": self.files})()

    def close(self):
        self.closed = True


def test_register_new_media_upserts_queued_document_and_closes_client(tmp_path, monkeypatch):
    staged = tmp_path / "stage" / "episode.mkv"
    staged.parent.mkdir()
    staged.write_bytes(b"media-bytes")
    dest_root = tmp_path / "library"
    destination = dest_root / "Series" / "Show" / "episode.mkv"
    files = FilesCollection()
    client = MongoClientDouble(files)
    ensure_calls = []
    monkeypatch.setattr(downloader, "MongoClient", lambda uri, **opts: _capture_client(client, uri, opts))
    monkeypatch.setattr(downloader, "ensure_mongo_parent_structure", lambda *args: ensure_calls.append(args))
    monkeypatch.setattr(downloader.time, "time", lambda: 1234)

    result = downloader.register_in_nebula_queue(
        staged, destination, dest_root, "mongodb://mongo.test", "nebula-test", "raphael"
    )

    assert result is True
    assert client.closed
    assert ensure_calls == [("mongodb://mongo.test", "nebula-test", "raphael", dest_root, destination.parent)]
    query, update, options = files.update_calls[0]
    assert query == {"parent": "/raphael/Series/Show", "name": "episode.mkv"}
    assert options == {"upsert": True}
    assert update["$set"] == {
        "type": "file",
        "name": "episode.mkv",
        "parent": "/raphael/Series/Show",
        "size": len(b"media-bytes"),
        "status": "queued",
        "local_path": str(staged),
        "mtime": 1234,
        "ctime": 1234,
        "parts": [],
        "delete_source": True,
        "search_name": "episode.mkv",
        "search_parent": "/raphael/series/show",
    }


def test_register_existing_local_path_updates_document_instead_of_upsert(tmp_path, monkeypatch):
    staged = tmp_path / "stage" / "film.mkv"
    staged.parent.mkdir()
    staged.write_bytes(b"replacement")
    dest_root = tmp_path / "library"
    destination = dest_root / "Filmes" / "Example" / "film.mkv"
    files = FilesCollection(existing={"_id": "mongo-file-id", "status": "failed"})
    client = MongoClientDouble(files)
    monkeypatch.setattr(downloader, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(downloader, "ensure_mongo_parent_structure", lambda *_args: None)
    monkeypatch.setattr(downloader.time, "time", lambda: 5678)

    result = downloader.register_in_nebula_queue(
        staged, destination, dest_root, "mongodb://mongo.test", "nebula-test", "raphael", delete_source=False
    )

    assert result is True
    assert client.closed
    query, update, options = files.update_calls[0]
    assert query == {"_id": "mongo-file-id"}
    assert options == {}
    assert update["$set"] == {
        "name": "film.mkv",
        "parent": "/raphael/Filmes/Example",
        "size": len(b"replacement"),
        "status": "queued",
        "mtime": 5678,
        "delete_source": False,
    }


def test_register_mongo_failure_returns_false_and_closes_client(tmp_path, monkeypatch):
    staged = tmp_path / "episode.mkv"
    staged.write_bytes(b"media")
    files = FilesCollection(fail_find=True)
    client = MongoClientDouble(files)
    monkeypatch.setattr(downloader, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(downloader, "ensure_mongo_parent_structure", lambda *_args: None)

    result = downloader.register_in_nebula_queue(
        staged,
        tmp_path / "library" / "episode.mkv",
        tmp_path / "library",
        "mongodb://mongo.test",
        "nebula-test",
    )

    assert result is False
    assert client.closed


def test_parent_creation_mongo_failure_is_nonfatal_and_closes_client(tmp_path, monkeypatch):
    files = FilesCollection(fail_find=True)
    client = MongoClientDouble(files)
    monkeypatch.setattr(downloader, "MongoClient", lambda *_args, **_kwargs: client)

    downloader.ensure_mongo_parent_structure(
        "mongodb://mongo.test",
        "nebula-test",
        "raphael",
        tmp_path / "library",
        tmp_path / "library" / "Series" / "Show",
    )

    assert client.closed


def test_parent_creation_inserts_missing_nested_directories_and_closes_client(tmp_path, monkeypatch):
    files = FilesCollection()
    client = MongoClientDouble(files)
    monkeypatch.setattr(downloader, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(downloader.time, "time", lambda: 4321)

    downloader.ensure_mongo_parent_structure(
        "mongodb://mongo.test",
        "nebula-test",
        "raphael",
        tmp_path / "library",
        tmp_path / "library" / "Series" / "Show",
    )

    assert [document["name"] for document in files.insert_calls] == ["Series", "Show"]
    assert [document["parent"] for document in files.insert_calls] == ["/raphael", "/raphael/Series"]
    assert all(document["type"] == "dir" and document["ctime"] == 4321 for document in files.insert_calls)
    assert client.closed


def test_media_validator_mongo_failure_closes_client_and_preserves_cached_snapshot(monkeypatch):
    files = FilesCollection(fail_scan=True)
    client = MongoClientDouble(files)
    monkeypatch.setattr(downloader, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(downloader.time, "time", lambda: 9999)
    validator = downloader.MediaValidator("mongodb://mongo.test", db_name="nebula-test")
    validator.completed_movies.add(("existing", "2025"))
    validator.exact_completed.add(("/raphael/filmes/existing", "existing.mkv"))
    validator.last_cache_time = 1111

    validator.refresh_cache(force=True)

    assert client.closed
    assert validator.completed_movies == {("existing", "2025")}
    assert validator.exact_completed == {("/raphael/filmes/existing", "existing.mkv")}
    assert validator.last_cache_time == 1111


def _capture_client(client: MongoClientDouble, uri: str, options: dict) -> MongoClientDouble:
    client.uri = uri
    client.options = options
    return client
