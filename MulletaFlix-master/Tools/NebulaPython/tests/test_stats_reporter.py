from __future__ import annotations

import asyncio
import importlib
import logging
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


class Files:
    def __init__(self, find_results=(), *, queued_insert_id="inserted"):
        self.find_results = iter(find_results)
        self.queued_insert_id = queued_insert_id
        self.find_queries = []
        self.updates = []

    async def find_one(self, query):
        self.find_queries.append(query)
        return next(self.find_results, None)

    async def update_one(self, query, update, **kwargs):
        self.updates.append((query, update, kwargs))
        inserted_id = self.queued_insert_id if "queue_identity" in query else None
        return SimpleNamespace(upserted_id=inserted_id)


class Queue:
    def __init__(self):
        self.items = []

    async def put(self, item):
        self.items.append(item)


def _configure(monkeypatch, files, queue, stage):
    monkeypatch.setattr(main, "STAGING_DIRS", [str(stage)])
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "resolve_media_parent", _parent)
    return SimpleNamespace(files=files)


async def _parent(_mongo, _parent, _filename):
    return "/raphael/Filmes"


@pytest.mark.asyncio
async def test_stats_reporter_registers_stable_file_and_queues_it(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "Movie.mkv"
    media.parent.mkdir()
    media.write_bytes(b"video")
    files, queue = Files(), Queue()
    mongo = _configure(monkeypatch, files, queue, stage)
    sleeps = []

    async def sleep(seconds):
        sleeps.append(seconds)
        if seconds == 5:
            raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", sleep)
    monkeypatch.setattr(main, "log_queue_state", _noop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert sleeps == [2, 5]
    assert queue.items == [{
        "path": str(media), "filename": "Movie.mkv", "parent": "/raphael/Filmes", "size": 5,
    }]
    file_document = next(update[1]["$setOnInsert"] for update in files.updates if "queue_identity" in update[0])
    assert file_document["status"] == "queued"
    assert file_document["search_name"] == "movie.mkv"
    assert file_document["search_parent"] == "/raphael/filmes"


@pytest.mark.asyncio
async def test_stats_reporter_does_not_queue_file_already_active_by_path(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "Movie.mkv"
    stage.mkdir()
    media.write_bytes(b"video")
    files = Files(find_results=[{"_id": "active", "status": "uploading"}])
    queue = Queue()
    mongo = _configure(monkeypatch, files, queue, stage)

    async def stop(_seconds):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert len(files.find_queries) == 1
    assert queue.items == []
    assert files.updates == []


@pytest.mark.asyncio
async def test_stats_reporter_duplicate_insert_result_is_idempotent_without_runtime_error(
    monkeypatch, tmp_path, caplog
):
    caplog.set_level(logging.DEBUG, logger=main.logger.name)
    stage = tmp_path / "stage"
    media = stage / "Movie.mkv"
    stage.mkdir()
    media.write_bytes(b"video")
    files, queue = Files(queued_insert_id=None), Queue()
    mongo = _configure(monkeypatch, files, queue, stage)
    sleeps = []

    async def sleep(seconds):
        sleeps.append(seconds)
        if seconds == 5:
            raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", sleep)
    monkeypatch.setattr(main, "log_queue_state", _noop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert len([update for update in files.updates if "queue_identity" in update[0]]) == 1
    assert queue.items == []
    assert "Já enfileirado por outro produtor: Movie.mkv" in caplog.text
    assert "Erro registro Movie.mkv" not in caplog.text


@pytest.mark.asyncio
async def test_stats_reporter_skips_partial_metadata_active_and_strm_contents(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    strm_dir = stage / "strm"
    strm_dir.mkdir(parents=True)
    active = stage / "active.mp4"
    active.write_bytes(b"video")
    (stage / "unfinished.mkv.partial").write_bytes(b"partial")
    (stage / "cover.nfo").write_text("metadata", encoding="utf-8")
    (strm_dir / "ignored.mkv").write_bytes(b"video")

    files, queue = Files(), Queue()
    mongo = _configure(monkeypatch, files, queue, stage)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", {str(active)})

    async def stop(_seconds):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert files.find_queries == []
    assert files.updates == []
    assert queue.items == []


@pytest.mark.asyncio
async def test_stats_reporter_requeues_failed_file_without_telegram_parts(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    stage.mkdir()
    media = stage / "retry.mkv"
    media.write_bytes(b"video")
    files = Files(find_results=[None, None, {"_id": "failed", "status": "failed", "parts": []}])
    queue = Queue()
    mongo = _configure(monkeypatch, files, queue, stage)
    monkeypatch.setattr(main, "log_queue_state", _noop)

    async def stop(_seconds):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert files.updates == [
        (
            {"_id": "failed"},
            {"$set": {
                "name": "retry.mkv",
                "parent": "/raphael/Filmes",
                "status": "queued",
                "local_path": str(media),
                "size": 5,
            }},
            {},
        )
    ]
    assert queue.items == [{
        "path": str(media), "filename": "retry.mkv", "parent": "/raphael/Filmes", "size": 5,
    }]


@pytest.mark.asyncio
async def test_stats_reporter_removes_staging_duplicate_when_catalog_is_completed(
    monkeypatch, tmp_path
):
    stage = tmp_path / "stage"
    stage.mkdir()
    media = stage / "already-sent.mkv"
    media.write_bytes(b"video")
    files = Files(find_results=[None, {"_id": "completed", "status": "completed"}])
    queue = Queue()
    mongo = _configure(monkeypatch, files, queue, stage)

    async def stop(_seconds):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert not media.exists()
    assert queue.items == []
    assert files.updates == []


@pytest.mark.asyncio
async def test_stats_reporter_removes_file_already_partially_sent_to_telegram(
    monkeypatch, tmp_path
):
    stage = tmp_path / "stage"
    stage.mkdir()
    media = stage / "partial-duplicate.mkv"
    media.write_bytes(b"video")
    files = Files(find_results=[
        None,
        None,
        {"_id": "partial", "status": "failed", "parts": [{"tg_message": 7}]},
    ])
    queue = Queue()
    mongo = _configure(monkeypatch, files, queue, stage)

    async def stop(_seconds):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop)

    with pytest.raises(asyncio.CancelledError):
        await main.stats_reporter(mongo)

    assert not media.exists()
    assert queue.items == []
    assert files.updates == []


async def _noop(*_args):
    return None
