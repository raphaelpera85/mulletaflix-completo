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

    async def find_one_and_update(self, query, update, **kwargs):
        self.queries.append((query, update, kwargs))
        if self.docs:
            return self.docs.pop(0)
        return None

    async def find_one(self, *_args, **_kwargs):
        return None

    async def update_one(self, query, update):
        self.updates.append((query, update))


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

    assert len(files.queries) == 2
    assert files.queries[0][0]["recovery_required"] == {"$ne": True}
    assert files.queries[0][1]["$set"]["status"] == "staging"
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


async def _resolved(value):
    return value


async def _parent():
    return "/raphael/Filmes/Movie"
