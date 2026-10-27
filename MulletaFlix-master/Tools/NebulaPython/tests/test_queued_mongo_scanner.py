from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


def _apply_update(document, update):
    for key, value in update.get("$set", {}).items():
        document[key] = value
    for key in update.get("$unset", {}):
        document.pop(key, None)


class Files:
    def __init__(self, docs):
        self.docs = list(docs)
        self.updates = []
        self.queries = []
        self.before_update = None

    async def find_one_and_update(self, query, update, **kwargs):
        self.queries.append((query, update, kwargs))
        for doc in self.docs:
            if query.get("status") == "uploading":
                lease_until = query.get("lease_until", {}).get("$lte")
                if doc.get("status") != "uploading" or lease_until is None or doc.get("lease_until", lease_until + 1) > lease_until:
                    continue
            elif query.get("status") == "queued":
                if doc.get("status") != "queued" or doc.get("recovery_required") is True:
                    continue
                retry_conditions = query.get("$or")
                if retry_conditions:
                    retry_at = doc.get("next_retry_at")
                    retry_deadline = retry_conditions[1]["next_retry_at"]["$lte"]
                    if retry_at is not None and retry_at > retry_deadline:
                        continue
            else:
                continue
            _apply_update(doc, update)
            return doc
        return None

    async def find_one(self, *_args, **_kwargs):
        return None

    async def update_one(self, query, update):
        self.updates.append((query, update))
        if self.before_update:
            self.before_update(query, update)
        for doc in self.docs:
            if all(doc.get(key) == value for key, value in query.items()):
                _apply_update(doc, update)


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
    doc = {"_id": "item", "name": "Movie.mkv", "local_path": str(media), "size": 5, "mtime": 1, "status": "queued"}
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))
    monkeypatch.setattr(main, "resolve_media_parent", lambda *_args: _parent())
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert len(files.queries) == 3
    assert files.queries[0][0]["status"] == "uploading"
    assert files.queries[1][0]["recovery_required"] == {"$ne": True}
    assert files.queries[1][1]["$set"]["status"] == "staging"
    assert queue.items == [{"path": str(media), "filename": "Movie.mkv", "parent": "/raphael/Filmes/Movie", "size": 5}]


@pytest.mark.asyncio
async def test_queued_scanner_marks_missing_local_file_for_manual_recovery(monkeypatch):
    doc = {"_id": "missing", "name": "Missing.mkv", "local_path": "Z:/stage/Missing.mkv", "size": 5, "status": "queued"}
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
async def test_queued_scanner_reclaims_only_expired_lease_and_preserves_progress(monkeypatch, tmp_path):
    media = tmp_path / "stalled.mkv"
    media.write_bytes(b"media")
    parts = [{"part_id": 0, "tg_file": "telegram-id", "file_size": 4}]
    doc = {
        "_id": "stalled",
        "name": "Stalled.mkv",
        "local_path": str(media),
        "size": 5,
        "mtime": 1,
        "status": "uploading",
        "lease_id": "old-lease",
        "lease_until": 100,
        "parts": parts,
    }
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))
    monkeypatch.setattr(main, "resolve_media_parent", lambda *_args: _parent())
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 101))
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert files.queries[0][0]["lease_until"] == {"$lte": 101}
    assert files.queries[0][1]["$set"]["status"] == "queued"
    assert files.queries[0][1]["$unset"]["lease_id"] == 1
    assert doc["parts"] == parts
    assert queue.items == [{
        "path": str(media),
        "filename": "Stalled.mkv",
        "parent": "/raphael/Filmes/Movie",
        "size": 5,
    }]


@pytest.mark.asyncio
async def test_queued_scanner_requeues_record_after_prequeue_error_with_backoff(monkeypatch, tmp_path):
    media = tmp_path / "retry.mkv"
    media.write_bytes(b"media")
    doc = {
        "_id": "retry",
        "name": "Retry.mkv",
        "local_path": str(media),
        "size": 5,
        "mtime": 1,
        "status": "queued",
    }
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))

    async def fail_parent(*_args):
        raise RuntimeError("temporary scanner failure")

    monkeypatch.setattr(main, "resolve_media_parent", fail_parent)
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 1000))
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert queue.items == []
    assert doc["status"] == "queued"
    assert doc["next_retry_at"] == 1060
    assert doc["failed_reason"] == "scanner_processing_error:RuntimeError"
    assert files.updates[-1][0] == {"_id": "retry", "status": "staging"}


@pytest.mark.asyncio
async def test_queued_scanner_does_not_requeue_record_already_claimed_by_worker(monkeypatch, tmp_path):
    media = tmp_path / "claimed.mkv"
    media.write_bytes(b"media")
    doc = {
        "_id": "claimed",
        "name": "Claimed.mkv",
        "local_path": str(media),
        "size": 5,
        "mtime": 1,
        "status": "queued",
    }
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))

    async def fail_parent(*_args):
        raise RuntimeError("temporary scanner failure")

    monkeypatch.setattr(main, "resolve_media_parent", fail_parent)
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 1000))

    def worker_claims_record(query, _update):
        if query == {"_id": "claimed", "status": "staging"}:
            doc["status"] = "uploading"

    files.before_update = worker_claims_record
    monkeypatch.setattr(main.asyncio, "sleep", _cancel_after_one_iteration)

    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert doc["status"] == "uploading"
    assert "next_retry_at" not in doc
    assert files.updates[-1][0] == {"_id": "claimed", "status": "staging"}


@pytest.mark.asyncio
async def test_queued_scanner_waits_until_retry_time_then_clears_retry_fields(monkeypatch, tmp_path):
    media = tmp_path / "deferred.mkv"
    media.write_bytes(b"media")
    doc = {
        "_id": "deferred",
        "name": "Deferred.mkv",
        "local_path": str(media),
        "size": 5,
        "mtime": 1,
        "status": "queued",
        "next_retry_at": 1060,
    }
    files, queue = Files([doc]), Queue()
    mongo = SimpleNamespace(files=files)
    now = 1059
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_local_path", lambda *_args: _resolved(str(media)))
    monkeypatch.setattr(main, "resolve_media_parent", lambda *_args: _parent())
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: now))

    sleep_count = 0

    async def set_now_and_cancel(_seconds):
        nonlocal sleep_count
        sleep_count += 1
        nonlocal now
        now = 1060
        if sleep_count == 2:
            raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", set_now_and_cancel)
    with pytest.raises(asyncio.CancelledError):
        await main.queued_mongo_scanner(mongo, max_workers=1)

    assert queue.items == [{
        "path": str(media),
        "filename": "Deferred.mkv",
        "parent": "/raphael/Filmes/Movie",
        "size": 5,
    }]
    assert "next_retry_at" not in doc


async def _resolved(value):
    return value


async def _parent():
    return "/raphael/Filmes/Movie"
