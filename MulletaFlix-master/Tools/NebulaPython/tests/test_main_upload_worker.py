from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


@pytest.mark.asyncio
async def test_parallel_upload_worker_completes_file_and_cleans_staging(monkeypatch, tmp_path):
    media = tmp_path / "episode.mkv"
    media.write_bytes(b"abcdefghij")
    file_doc = {"_id": "file-1", "status": "queued", "parts": []}
    updates = []
    claims = []
    uploaded = []
    removed = []

    class Files:
        async def find_one(self, query):
            return file_doc

        async def find_one_and_update(self, query, update, *, return_document):
            claims.append((query, update, return_document))
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))

    queue = asyncio.Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    monkeypatch.setattr(main, "PART_WORKERS_PER_FILE", 2)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "series")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "caption")

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main.Metrics, "log_success", lambda size: uploaded.append(("success", size)))
    monkeypatch.setattr(main.Metrics, "log_fail", lambda: uploaded.append(("failure", None)))
    monkeypatch.setattr(main.uuid, "uuid4", lambda: "stable-uuid")

    async def upload_part(_worker_id, _bots, _chat, _path, file_uuid, part_num, chunk_data, *_args, **_kwargs):
        uploaded.append((part_num, chunk_data, file_uuid))
        return {
            "part_id": part_num,
            "file_size": len(chunk_data),
            "chunk_name": f"{file_uuid}.part_{part_num:03d}",
            "bot_name": "bot-a",
        }

    monkeypatch.setattr(main, "upload_part_with_retries", upload_part)
    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(
        main.upload_worker_parallel([SimpleNamespace(name="bot-a")], -100, SimpleNamespace(files=Files()), 7)
    )

    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert len(claims) == 1
    assert [entry[:2] for entry in uploaded if isinstance(entry[0], int)] == [
        (0, b"abcd"),
        (1, b"efgh"),
        (2, b"ij"),
    ]
    assert len([update for _query, update in updates if "$set" in update and "status" not in update["$set"]]) == 2
    final_update = updates[-1][1]
    assert final_update["$set"]["status"] == "completed"
    assert final_update["$set"]["part_count"] == 3
    assert final_update["$set"]["size"] == 10
    assert final_update["$set"]["obfuscated_id"] == "stable-uuid"
    assert final_update["$unset"] == {"uploadId": 1, "local_path": 1}
    assert uploaded[-1] == ("success", 10)
    assert removed == [str(media)]
    assert str(media) not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_parallel_upload_worker_cancels_blocked_readahead_after_upload_failure(monkeypatch, tmp_path):
    media = tmp_path / "large-episode.mkv"
    media.write_bytes(b"x" * 40)
    file_doc = {"_id": "file-fail", "status": "queued", "parts": []}
    updates = []
    producer_tasks = []

    class Files:
        async def find_one(self, _query):
            return file_doc

        async def find_one_and_update(self, _query, _update, *, return_document):
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))

    queue = asyncio.Queue()
    original_producer = main._readahead_producer

    async def track_producer(*args, **kwargs):
        producer_tasks.append(asyncio.current_task())
        await original_producer(*args, **kwargs)

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    async def fail_upload(*_args, **_kwargs):
        raise RuntimeError("simulated Telegram failure")

    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    monkeypatch.setattr(main, "PART_WORKERS_PER_FILE", 2)
    monkeypatch.setattr(main, "_readahead_producer", track_producer)
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "series")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "caption")
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "upload_part_with_retries", fail_upload)
    monkeypatch.setattr(main.Metrics, "log_fail", lambda: None)
    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(
        main.upload_worker_parallel([SimpleNamespace(name="bot")], -100, SimpleNamespace(files=Files()), 8)
    )

    try:
        await asyncio.wait_for(queue.join(), timeout=3)
        await asyncio.sleep(0)
        assert len(producer_tasks) == 1
        assert producer_tasks[0].done(), "read-ahead producer must not outlive a failed upload"
        assert any(update.get("$set", {}).get("status") == "failed" for _query, update in updates)
        assert str(media) not in main.ACTIVE_UPLOADS
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker
        for producer in producer_tasks:
            if not producer.done():
                producer.cancel()
        if producer_tasks:
            await asyncio.gather(*producer_tasks, return_exceptions=True)


@pytest.mark.asyncio
async def test_small_file_worker_uploads_single_payload_and_persists_completion(monkeypatch, tmp_path):
    result = await _run_small_file_worker(monkeypatch, tmp_path, fail_upload=False)

    assert result["payloads"] == [("S3", b"cover-bytes", 0)]
    final_update = result["updates"][-1][1]
    assert final_update["$set"]["status"] == "completed"
    assert final_update["$set"]["part_count"] == 1
    assert final_update["$set"]["size"] == len(b"cover-bytes")
    assert final_update["$unset"] == {"uploadId": 1, "local_path": 1}
    assert result["removed"] == [result["path"]]
    assert result["metrics"] == [("success", len(b"cover-bytes"))]
    assert result["path"] not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_small_file_worker_marks_upload_failure_without_deleting_source(monkeypatch, tmp_path):
    result = await _run_small_file_worker(monkeypatch, tmp_path, fail_upload=True)

    assert any(update.get("$set", {}).get("status") == "failed" for _query, update in result["updates"])
    assert result["removed"] == []
    assert result["metrics"] == [("failure", None)]
    assert result["path"] not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_small_file_worker_reroutes_file_above_size_limit(monkeypatch, tmp_path):
    media = tmp_path / "misrouted.bin"
    media.write_bytes(b"12345")
    requeued = []

    class Queue:
        def __init__(self):
            self.items = asyncio.Queue()

        async def get_small(self):
            return await self.items.get()

        async def put(self, item):
            requeued.append(item)

        def task_done(self):
            self.items.task_done()

        async def join(self):
            await self.items.join()

    async def unexpected(*_args, **_kwargs):
        pytest.fail("oversized file must be routed without database or Telegram access")

    queue = Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "SMALL_FILE_MAX_BYTES", 4)
    monkeypatch.setattr(main, "resolve_media_parent", unexpected)
    monkeypatch.setattr(main, "upload_part_with_retries", unexpected)
    await queue.items.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(main.upload_small_file_worker([object()], -100, object(), 4))

    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert requeued == [{"path": str(media), "filename": media.name, "parent": "Series", "size": 5}]
    assert str(media) not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("case", "expected_find_calls", "expected_removed"),
    [
        ("partial", 0, False),
        ("missing", 0, False),
        ("empty", 0, True),
        ("metadata_missing", 2, False),
    ],
)
async def test_small_file_worker_skips_invalid_staging_inputs(
    monkeypatch, tmp_path, case, expected_find_calls, expected_removed
):
    filename = "unfinished.partial" if case == "partial" else "small.bin"
    media = tmp_path / filename
    if case == "empty":
        media.write_bytes(b"")
    elif case != "missing":
        media.write_bytes(b"content")

    find_calls = []
    removed = []

    class Files:
        async def find_one(self, query):
            find_calls.append(query)
            return None

        async def find_one_and_update(self, *_args, **_kwargs):
            pytest.fail("no metadata may be claimed for invalid staging input")

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    queue = asyncio.Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main, "upload_part_with_retries", pytest.fail)
    await queue.put({"path": str(media), "filename": filename, "parent": "Series"})
    worker = asyncio.create_task(main.upload_small_file_worker([object()], -100, SimpleNamespace(files=Files()), 5))

    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert len(find_calls) == expected_find_calls
    assert (removed == [str(media)]) is expected_removed
    assert str(media) not in main.ACTIVE_UPLOADS


async def _run_small_file_worker(monkeypatch, tmp_path, *, fail_upload):
    media = tmp_path / "cover.jpg"
    media.write_bytes(b"cover-bytes")
    file_doc = {"_id": "small-file", "status": "queued", "parts": []}
    updates = []
    payloads = []
    removed = []
    metrics = []

    class Files:
        async def find_one(self, _query):
            return file_doc

        async def find_one_and_update(self, _query, _update, *, return_document):
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))

    async def resolve_parent(*_args):
        return "Books"

    async def log_state(*_args):
        return None

    async def upload_part(worker_id, _bots, _chat, _path, _uuid, part_num, payload, *_args, **_kwargs):
        payloads.append((worker_id, payload, part_num))
        if fail_upload:
            raise RuntimeError("simulated small-file upload failure")
        return {"part_id": part_num, "file_size": len(payload), "bot_name": "bot-a"}

    queue = asyncio.Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "book")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "caption")
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "upload_part_with_retries", upload_part)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main.Metrics, "log_success", lambda size: metrics.append(("success", size)))
    monkeypatch.setattr(main.Metrics, "log_fail", lambda: metrics.append(("failure", None)))
    await queue.put({"path": str(media), "filename": media.name, "parent": "Books"})
    worker = asyncio.create_task(
        main.upload_small_file_worker([SimpleNamespace(name="bot-a")], -100, SimpleNamespace(files=Files()), 3)
    )
    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    return {
        "path": str(media),
        "updates": updates,
        "payloads": payloads,
        "removed": removed,
        "metrics": metrics,
    }
