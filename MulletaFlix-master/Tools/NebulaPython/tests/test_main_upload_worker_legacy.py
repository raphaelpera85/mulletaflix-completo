from __future__ import annotations

import asyncio
import importlib
import io
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


@pytest.mark.asyncio
@pytest.mark.parametrize("case", ["partial", "missing", "empty"])
async def test_legacy_upload_worker_releases_invalid_staging_items(monkeypatch, tmp_path, case):
    media = tmp_path / ("unfinished.partial" if case == "partial" else "empty.mkv")
    if case == "partial":
        media.write_bytes(b"incomplete")
    elif case == "empty":
        media.write_bytes(b"")
    database_lookups = []
    removed = []
    queue = asyncio.Queue()

    class Files:
        async def find_one(self, query):
            database_lookups.append(query)
            pytest.fail("invalid staging input must not query MongoDB")

    async def resolve_parent(*_args):
        pytest.fail("invalid staging input must be rejected before parent resolution")

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(main.upload_worker(object(), -100, SimpleNamespace(files=Files()), 20))
    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert database_lookups == []
    assert removed == ([str(media)] if case == "empty" else [])
    assert str(media) not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_legacy_upload_worker_persists_each_part_and_completion(monkeypatch, tmp_path):
    media = tmp_path / "legacy-episode.mkv"
    media.write_bytes(b"abcdefg")
    file_doc = {"_id": "legacy-file", "status": "queued", "parts": []}
    updates = []
    sent_payloads = []
    removed = []
    queue = asyncio.Queue()

    class Files:
        async def find_one(self, query):
            assert query in (
                {"name": media.name, "parent": "Series"},
                {"name": media.name, "local_path": str(media)},
            )
            return file_doc

        async def find_one_and_update(self, query, update, *, return_document):
            assert query == {"_id": "legacy-file", "status": {"$in": ["queued", "staging"]}}
            assert update["$set"]["status"] == "uploading"
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))

    class Bot:
        name = "legacy-bot"

        async def send_document(self, **kwargs):
            payload = kwargs["document"]
            assert isinstance(payload, io.BytesIO)
            sent_payloads.append((kwargs["file_name"], payload.read(), kwargs["caption"]))
            return SimpleNamespace(document=SimpleNamespace(file_id=f"tg-{len(sent_payloads)}"), id=100 + len(sent_payloads))

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "CHUNK_SIZE", 3)
    monkeypatch.setattr(main, "MAX_RETRIES", 1)
    monkeypatch.setattr(main, "UPLOAD_STATUS_MESSAGES", False)
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "series")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "episode caption")
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main.uuid, "uuid4", lambda: "legacy-uuid")
    monkeypatch.setattr(main.Metrics, "log_success", lambda _size: None)
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)

    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(main.upload_worker(Bot(), -100, SimpleNamespace(files=Files()), 21))
    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert [(name, payload) for name, payload, _caption in sent_payloads] == [
        ("legacy-uuid.part_000", b"abc"),
        ("legacy-uuid.part_001", b"def"),
        ("legacy-uuid.part_002", b"g"),
    ]
    final_query, final_update = updates[-1]
    assert final_query == {"_id": "legacy-file"}
    persisted = final_update["$set"]
    assert persisted["status"] == "completed"
    assert persisted["part_count"] == 3
    assert persisted["size"] == 7
    assert [part["tg_file"] for part in persisted["parts"]] == ["tg-1", "tg-2", "tg-3"]
    assert [(part["byte_start"], part["byte_end"]) for part in persisted["parts"]] == [(0, 2), (3, 5), (6, 6)]
    assert final_update["$unset"] == {"uploadId": 1, "local_path": 1}
    assert removed == [str(media)]
    assert str(media) not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_legacy_upload_worker_marks_telegram_failure_and_keeps_source(monkeypatch, tmp_path):
    media = tmp_path / "retry-failure.mkv"
    media.write_bytes(b"data")
    file_doc = {"_id": "failed-file", "status": "queued", "parts": []}
    updates = []
    removed = []
    failures = []
    queue = asyncio.Queue()

    class Files:
        async def find_one(self, _query):
            return file_doc

        async def find_one_and_update(self, *_args, **_kwargs):
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))

    class Bot:
        async def send_document(self, **_kwargs):
            raise RuntimeError("telegram unavailable")

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    async def no_wait(_seconds):
        return None

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "CHUNK_SIZE", 3)
    monkeypatch.setattr(main, "MAX_RETRIES", 1)
    monkeypatch.setattr(main, "UPLOAD_STATUS_MESSAGES", False)
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "series")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "episode caption")
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main, "upload_part_with_retries", pytest.fail)
    monkeypatch.setattr(main.Metrics, "log_fail", lambda: failures.append(True))
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)

    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(main.upload_worker(Bot(), -100, SimpleNamespace(files=Files()), 22))
    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert any(update.get("$set", {}).get("status") == "failed" for _query, update in updates)
    assert failures == [True]
    assert removed == []
    assert media.read_bytes() == b"data"
    assert str(media) not in main.ACTIVE_UPLOADS
