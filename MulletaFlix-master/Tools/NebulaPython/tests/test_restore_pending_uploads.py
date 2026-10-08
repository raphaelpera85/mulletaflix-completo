from __future__ import annotations

import importlib
from pathlib import Path
from types import SimpleNamespace

import pytest
from pymongo.errors import DuplicateKeyError

main = importlib.import_module("main")


class AsyncCursor:
    def __init__(self, docs):
        self.docs = docs

    def __aiter__(self):
        self.iterator = iter(self.docs)
        return self

    async def __anext__(self):
        try:
            return next(self.iterator)
        except StopIteration:
            raise StopAsyncIteration


class Files:
    def __init__(self, docs, *, duplicate=None, duplicate_key_on_staging=False):
        self.docs = docs
        self.duplicate = duplicate
        self.duplicate_key_on_staging = duplicate_key_on_staging
        self.updates = []

    def find(self, query, *_args):
        assert query["status"]["$in"] == ["queued", "uploading", "staging"]
        return AsyncCursor(self.docs)

    async def find_one(self, query, *_args):
        if "$ne" in query.get("_id", {}) and self.duplicate:
            return self.duplicate
        return None

    async def update_one(self, query, update):
        self.updates.append((query, update))
        if self.duplicate_key_on_staging and update.get("$set", {}).get("status") == "staging":
            self.duplicate_key_on_staging = False
            raise DuplicateKeyError("simulated unique target collision")


class Queue:
    def __init__(self):
        self.items = []

    async def put(self, item):
        self.items.append(item)


def configure_restore(monkeypatch, files, queue, local_paths):
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "cleanup_duplicate_target_records", _noop)
    monkeypatch.setattr(main, "resolve_local_path", lambda _mongo, doc: _resolved(local_paths, doc))
    monkeypatch.setattr(main, "resolve_media_parent", lambda *_args: _parent())
    monkeypatch.setattr(main, "log_queue_state", _noop)
    return mongo


async def _noop(*_args):
    return None


async def _parent():
    return "/raphael/Filmes/Example"


async def _resolved(paths, doc):
    return paths.get(doc["_id"])


@pytest.mark.asyncio
async def test_restore_pending_uploads_requeues_existing_media_in_oldest_first_order(monkeypatch, tmp_path):
    older_path = tmp_path / "older.mkv"
    newer_path = tmp_path / "newer.mkv"
    older_path.write_bytes(b"old")
    newer_path.write_bytes(b"new")
    docs = [
        {"_id": "new", "name": "Newer.mkv", "local_path": str(newer_path), "size": 3, "mtime": 20, "status": "queued"},
        {"_id": "old", "name": "Older.mkv", "local_path": str(older_path), "size": 3, "mtime": 10, "status": "uploading"},
    ]
    files, queue = Files(docs), Queue()
    mongo = configure_restore(monkeypatch, files, queue, {"old": str(older_path), "new": str(newer_path)})

    await main.restore_pending_uploads(mongo)

    assert [item["filename"] for item in queue.items] == ["Older.mkv", "Newer.mkv"]
    assert [item["parent"] for item in queue.items] == ["/raphael/Filmes/Example"] * 2
    assert [update[1]["$set"]["status"] for update in files.updates] == ["staging", "staging"]


@pytest.mark.asyncio
async def test_restore_pending_uploads_marks_missing_stage_for_recovery_without_enqueuing(monkeypatch):
    doc = {"_id": "missing", "name": "Lost.mkv", "local_path": "Z:/stage/Lost.mkv", "status": "queued"}
    files, queue = Files([doc]), Queue()
    mongo = configure_restore(monkeypatch, files, queue, {})

    await main.restore_pending_uploads(mongo)

    assert queue.items == []
    assert files.updates == [(
        {"_id": "missing"},
        {"$set": {
            "status": "queued",
            "failed_at": files.updates[0][1]["$set"]["failed_at"],
            "failed_reason": "local_path_missing",
            "recovery_required": True,
        }},
    )]


@pytest.mark.asyncio
async def test_restore_pending_uploads_preserves_duplicate_destination(monkeypatch, tmp_path):
    media_path = tmp_path / "existing.mkv"
    media_path.write_bytes(b"media")
    doc = {"_id": "retry", "name": "Existing.mkv", "local_path": str(media_path), "size": 5, "mtime": 10, "status": "queued"}
    files = Files([doc], duplicate={"_id": "other"})
    queue = Queue()
    mongo = configure_restore(monkeypatch, files, queue, {"retry": str(media_path)})

    await main.restore_pending_uploads(mongo)

    assert queue.items == []
    assert files.updates == [({"_id": "retry"}, {"$set": {
        "status": "queued", "failed_reason": "duplicate_target_preserved", "recovery_required": True,
    }})]


@pytest.mark.asyncio
async def test_restore_pending_uploads_handles_racing_duplicate_key_without_losing_stage(monkeypatch, tmp_path):
    media_path = tmp_path / "racing.mkv"
    media_path.write_bytes(b"media")
    doc = {"_id": "racing", "name": "Racing.mkv", "local_path": str(media_path), "size": 5, "mtime": 10, "status": "queued"}
    files = Files([doc], duplicate_key_on_staging=True)
    queue = Queue()
    mongo = configure_restore(monkeypatch, files, queue, {"racing": str(media_path)})

    await main.restore_pending_uploads(mongo)

    assert queue.items == []
    assert files.updates[-1] == ({"_id": "racing"}, {"$set": {
        "status": "queued", "failed_reason": "duplicate_target_preserved", "recovery_required": True,
    }})

