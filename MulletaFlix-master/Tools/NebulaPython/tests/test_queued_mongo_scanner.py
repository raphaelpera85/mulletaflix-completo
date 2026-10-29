from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


class Files:
    def __init__(self, docs):
        self.docs = list(docs)
        self.updates = []
        self.queries = []
        self.fail_after_claim_commit = False

    def find(self, query, *_args, **_kwargs):
        return Cursor([doc for doc in self.docs if _matches(doc, query)])

    async def find_one_and_update(self, query, update, **kwargs):
        self.queries.append((query, update, kwargs))
        doc = next((item for item in self.docs if _matches(item, query)), None)
        if doc is not None:
            _apply_update(doc, update)
            if self.fail_after_claim_commit:
                self.fail_after_claim_commit = False
                raise TimeoutError("claim committed but response timed out")
            return dict(doc)
        return None

    async def find_one(self, query, *_args, **_kwargs):
        return next((dict(doc) for doc in self.docs if _matches(doc, query)), None)

    async def update_one(self, query, update):
        self.updates.append((query, update))
        doc = next((item for item in self.docs if _matches(item, query)), None)
        if doc is not None:
            _apply_update(doc, update)


class Cursor:
    def __init__(self, docs):
        self.docs = docs

    def __aiter__(self):
        self._iterator = iter(self.docs)
        return self

    async def __anext__(self):
        try:
            return self._iterator.__next__()
        except StopIteration:
            raise StopAsyncIteration


def _matches(doc, query):
    for key, expected in query.items():
        if key == "$or":
            if not any(_matches(doc, option) for option in expected):
                return False
            continue
        actual = doc.get(key)
        if isinstance(expected, dict):
            for operator, value in expected.items():
                if operator == "$exists" and ((key in doc) != value):
                    return False
                if operator == "$ne" and actual == value:
                    return False
                if operator == "$lte" and (actual is None or actual > value):
                    return False
                if operator == "$gt" and (actual is None or actual <= value):
                    return False
                if operator == "$in" and actual not in value:
                    return False
        elif actual != expected:
            return False
    return True


def _apply_update(doc, update):
    doc.update(update.get("$set", {}))
    for key in update.get("$unset", {}):
        doc.pop(key, None)


class Queue:
    def __init__(self):
        self.items = []

    def large_qsize(self):
        return 10

    async def put(self, item):
        self.items.append(item)


async def _cancel_after_one_iteration(_seconds):
    raise asyncio.CancelledError


@pytest.mark.asyncio
async def test_queued_scanner_claims_oldest_item_and_enqueues_existing_stage(monkeypatch, tmp_path):
    media = tmp_path / "movie.mkv"
    media.write_bytes(b"media")
    doc = {"_id": "item", "type": "file", "name": "Movie.mkv", "local_path": str(media), "size": 5, "mtime": 1, "status": "queued"}
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))
    monkeypatch.setattr(main, "resolve_media_parent", lambda *_args: _parent())
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert len(files.queries) == 2
    assert files.queries[0][0]["recovery_required"] == {"$ne": True}
    assert files.queries[0][1]["$set"]["status"] == "staging"
    assert queue.items == [{"path": str(media), "filename": "Movie.mkv", "parent": "/raphael/Filmes/Movie", "size": 5}]
    assert files.docs[0]["status"] == "queued"
    assert files.docs[0]["scanner_enqueued"] is True


@pytest.mark.asyncio
async def test_queued_scanner_marks_missing_local_file_for_manual_recovery(monkeypatch):
    doc = {"_id": "missing", "type": "file", "name": "Missing.mkv", "local_path": "Z:/stage/Missing.mkv", "size": 5, "status": "queued"}
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(None))
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert queue.items == []
    assert files.updates[0][0] == {"_id": "missing"}
    assert files.updates[0][1]["$set"]["failed_reason"] == "local_path_missing"
    assert files.updates[0][1]["$set"]["recovery_required"] is True


@pytest.mark.asyncio
async def test_queued_scanner_requeues_claim_after_transient_processing_error(monkeypatch, tmp_path):
    media = tmp_path / "movie.mkv"
    media.write_bytes(b"media")
    doc = {"_id": "transient", "type": "file", "name": "Movie.mkv", "local_path": str(media), "size": 5, "status": "queued"}
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)

    async def fail_once(*_args):
        raise TimeoutError("temporary path lookup failure")

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", fail_once)
    monkeypatch.setattr(main.time, "time", lambda: 100)
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    query, update = files.updates[0]
    assert query["_id"] == "transient"
    assert query["status"] == "staging"
    assert "scanner_claim_id" in query
    assert update["$set"]["status"] == "queued"
    assert update["$set"]["failed_reason"] == "queue_scanner_transient_error"
    assert update["$set"]["scanner_retry_after"] == 105
    assert queue.items == []
    assert files.queries[0][0]["$or"] == [
        {"scanner_retry_after": {"$exists": False}},
        {"scanner_retry_after": {"$lte": 100}},
    ]


@pytest.mark.asyncio
async def test_queued_scanner_retries_staging_release_after_mongo_recovers(monkeypatch, tmp_path):
    media = tmp_path / "movie.mkv"
    media.write_bytes(b"media")

    class FilesWithTransientUpdate(Files):
        async def update_one(self, query, update):
            self.updates.append((query, update))
            if len(self.updates) == 1:
                raise ConnectionError("temporary MongoDB outage")

    files, queue = FilesWithTransientUpdate([{
        "_id": "mongo-recovery", "type": "file", "name": "Movie.mkv", "local_path": str(media), "size": 5, "status": "queued"
    }]), Queue()
    mongo = SimpleNamespace(files=files)
    clock = [100]
    sleeps = [0]

    async def fail_processing(*_args):
        raise TimeoutError("temporary path lookup failure")

    async def advance_clock(_seconds):
        sleeps[0] += 1
        clock[0] += 6
        if sleeps[0] == 2:
            raise asyncio.CancelledError

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", fail_processing)
    monkeypatch.setattr(main.time, "time", lambda: clock[0])
    monkeypatch.setattr(main.asyncio, "sleep", advance_clock)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert len(files.updates) == 2
    assert all(query["_id"] == "mongo-recovery" and query["status"] == "staging" and "scanner_claim_id" in query for query, _ in files.updates)
    assert files.updates[1][1]["$set"]["status"] == "queued"
    assert files.updates[1][1]["$set"]["scanner_retry_after"] == 111
    assert queue.items == []


@pytest.mark.asyncio
async def test_queued_scanner_recovers_claim_committed_before_timeout(monkeypatch, tmp_path):
    media = tmp_path / "movie.mkv"
    media.write_bytes(b"media")
    files = Files([{
        "_id": "claim-timeout", "name": "Movie.mkv", "local_path": str(media), "size": 5,
        "status": "queued", "type": "file",
    }])
    files.fail_after_claim_commit = True
    queue = Queue()
    mongo = SimpleNamespace(files=files)
    clock = [100]
    sleeps = [0]

    async def advance_clock(_seconds):
        sleeps[0] += 1
        clock[0] += 6
        if sleeps[0] == 2:
            raise asyncio.CancelledError

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main.time, "time", lambda: clock[0])
    monkeypatch.setattr(main.asyncio, "sleep", advance_clock)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    doc = files.docs[0]
    assert doc["status"] == "queued"
    assert doc["failed_reason"] == "queue_scanner_transient_error"
    assert doc["scanner_retry_after"] == 111
    assert "scanner_claim_id" not in doc
    assert queue.items == []


@pytest.mark.asyncio
async def test_queued_scanner_recovers_stale_claim_from_previous_process(monkeypatch, tmp_path):
    media = tmp_path / "movie.mkv"
    media.write_bytes(b"media")
    files = Files([{
        "_id": "previous-process-claim", "name": "Movie.mkv", "local_path": str(media), "size": 5,
        "status": "staging", "type": "file", "scanner_claim_id": "old-scanner",
        "scanner_claimed_at": 90, "scanner_enqueued": False,
    }])
    queue = Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main.time, "time", lambda: 100)
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    doc = files.docs[0]
    assert doc["status"] == "queued"
    assert doc["scanner_retry_after"] == 105
    assert "scanner_claim_id" not in doc
    assert files.updates[0][0]["scanner_claim_id"] == "old-scanner"
    assert queue.items == []


@pytest.mark.asyncio
async def test_queued_scanner_backs_off_after_each_failed_staging_recovery(monkeypatch):
    class FilesWithPersistentUpdateFailure(Files):
        async def update_one(self, query, update):
            self.updates.append((query, update))
            raise ConnectionError("MongoDB remains unavailable")

    files = FilesWithPersistentUpdateFailure([{
        "_id": "staging-retry", "name": "Movie.mkv", "local_path": "stage/Movie.mkv", "size": 5,
        "status": "staging", "type": "file", "scanner_claim_id": "old-scanner",
        "scanner_claimed_at": 90, "scanner_enqueued": False,
    }])
    clock = [100]
    sleeps = [0]

    async def advance_clock(_seconds):
        sleeps[0] += 1
        clock[0] += 1
        if sleeps[0] == 7:
            raise asyncio.CancelledError

    monkeypatch.setattr(main, "UPLOAD_QUEUE", Queue())
    monkeypatch.setattr(main.time, "time", lambda: clock[0])
    monkeypatch.setattr(main.asyncio, "sleep", advance_clock)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(SimpleNamespace(files=files), max_workers=1)

    assert len(files.updates) == 2
    assert [update["$set"]["scanner_retry_after"] for _, update in files.updates] == [105, 110]


async def _resolved(value):
    return value


async def _parent():
    return "/raphael/Filmes/Movie"
