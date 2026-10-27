from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


def _matches(document, query):
    for key, expected in query.items():
        actual = document.get(key)
        if isinstance(expected, dict) and "$gt" in expected:
            if actual is None or actual <= expected["$gt"]:
                return False
        elif actual != expected:
            return False
    return True


def _apply_update(document, update):
    document.update(update.get("$set", {}))
    for key in update.get("$unset", {}):
        document.pop(key, None)


def _matched_result(matched):
    return SimpleNamespace(matched_count=int(matched))


@pytest.mark.asyncio
async def test_part_retry_stops_before_sending_again_after_lease_loss(monkeypatch, tmp_path):
    sends = []
    lease_checks = []

    async def send_part(*_args, **_kwargs):
        sends.append(True)
        raise TimeoutError("ambiguous Telegram response")

    async def check_lease():
        lease_checks.append(True)
        if len(lease_checks) == 3:
            raise main.UploadLeaseLostError("claim generation changed during retry wait")

    sleeps = []
    async def no_wait(_seconds):
        sleeps.append(True)

    monkeypatch.setattr(main, "MAX_RETRIES", 3)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    monkeypatch.setattr(main, "_send_part_on_idle_bot", send_part)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)

    async def confirmed_absent():
        return main.TelegramPartReconciliation(main.TelegramPartReconciliationStatus.NOT_FOUND)

    with pytest.raises(main.UploadLeaseLostError, match="during retry wait"):
        await main.upload_part_with_retries(
            1,
            [SimpleNamespace(name="bot-a")],
            -100,
            str(tmp_path / "part.bin"),
            "media-uuid",
            0,
            chunk_data=b"bytes",
            lease_check=check_lease,
            reconcile=confirmed_absent,
        )

    assert sends == [True]
    assert len(lease_checks) == 3
    assert sleeps == [True]


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
            if not _matches(file_doc, {key: value for key, value in query.items() if key != "status"}):
                return None
            if file_doc.get("status") not in query["status"]["$in"]:
                return None
            _apply_update(file_doc, update)
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))
            matched = _matches(file_doc, query)
            if matched:
                _apply_update(file_doc, update)
            return _matched_result(matched)

    queue = asyncio.Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "UPLOAD_CHECKPOINT_JOURNAL", None)
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
    class StableUUID:
        hex = "lease-token"

        def __str__(self):
            return "stable-uuid"

    monkeypatch.setattr(main.uuid, "uuid4", StableUUID)

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
    assert len([update for _query, update in updates if "$set" in update and "status" not in update["$set"]]) == 3
    final_update = updates[-1][1]
    assert final_update["$set"]["status"] == "completed"
    assert final_update["$set"]["part_count"] == 3
    assert final_update["$set"]["size"] == 10
    assert final_update["$set"]["obfuscated_id"] == "stable-uuid"
    assert final_update["$unset"] == {
        "worker_id": 1,
        "lease_id": 1,
        "lease_until": 1,
        "heartbeat_at": 1,
        "uploadId": 1,
        "local_path": 1,
    }
    assert uploaded[-1] == ("success", 10)
    assert removed == [str(media)]
    assert str(media) not in main.ACTIVE_UPLOADS


@pytest.mark.asyncio
async def test_parallel_worker_stops_when_same_worker_reclaims_new_lease(monkeypatch, tmp_path):
    media = tmp_path / "reclaimed.mkv"
    media.write_bytes(b"abcdefghij")
    file_doc = {"_id": "file-reclaimed", "status": "queued", "parts": []}
    updates = []
    payloads = []
    removed = []

    class Files:
        async def find_one(self, _query):
            return file_doc

        async def find_one_and_update(self, _query, update, *, return_document):
            _apply_update(file_doc, update)
            return dict(file_doc)

        async def update_one(self, query, update):
            matched = _matches(file_doc, query)
            updates.append((query, update, matched))
            if matched:
                _apply_update(file_doc, update)
            return _matched_result(matched)

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    async def reclaim_during_send(worker_id, _bots, _chat, _path, file_uuid, part_num, chunk_data, *_args, **_kwargs):
        payloads.append((worker_id, part_num))
        # Another claim reuses the same worker_id but owns a new generation.
        file_doc["lease_id"] = "new-generation"
        return {"part_id": part_num, "file_size": len(chunk_data), "chunk_name": f"{file_uuid}.part_{part_num:03d}"}

    queue = asyncio.Queue()
    monkeypatch.setattr(main, "UPLOAD_QUEUE", queue)
    monkeypatch.setattr(main, "ACTIVE_UPLOADS", set())
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    monkeypatch.setattr(main, "PART_WORKERS_PER_FILE", 1)
    monkeypatch.setattr(main, "resolve_media_parent", resolve_parent)
    monkeypatch.setattr(main, "classify_media_type", lambda *_args: "series")
    monkeypatch.setattr(main, "build_part_caption", lambda *_args: "caption")
    monkeypatch.setattr(main, "log_queue_state", log_state)
    monkeypatch.setattr(main, "safe_remove_staging_file", lambda path, **_kwargs: removed.append(path))
    monkeypatch.setattr(main, "upload_part_with_retries", reclaim_during_send)
    monkeypatch.setattr(main.Metrics, "log_fail", lambda: None)
    await queue.put({"path": str(media), "filename": media.name, "parent": "Series"})
    worker = asyncio.create_task(
        main.upload_worker_parallel([SimpleNamespace(name="bot-a")], -100, SimpleNamespace(files=Files()), 9)
    )

    try:
        await asyncio.wait_for(queue.join(), timeout=3)
    finally:
        worker.cancel()
        with pytest.raises(asyncio.CancelledError):
            await worker

    assert payloads == [(9, 0)]
    assert any(not matched and query["worker_id"] == "9" and query["lease_id"] != "new-generation" for query, _update, matched in updates)
    assert file_doc["status"] == "uploading"
    assert file_doc["lease_id"] == "new-generation"
    assert removed == []


@pytest.mark.asyncio
async def test_upload_lease_heartbeat_detects_reclaimed_generation(monkeypatch):
    claimed_doc = {
        "_id": "file-lease",
        "name": "test.mkv",
        "status": "uploading",
        "worker_id": "3",
        "lease_id": "old-generation",
        "lease_until": int(main.time.time()) + 300,
    }
    database_doc = {**claimed_doc, "lease_id": "new-generation"}
    queries = []

    class Files:
        async def update_one(self, query, _update):
            queries.append(query)
            return _matched_result(_matches(database_doc, query))

    monkeypatch.setattr(main, "UPLOAD_LEASE_HEARTBEAT_SECONDS", 0.001)
    heartbeat = await main._start_upload_lease_heartbeat(SimpleNamespace(files=Files()), claimed_doc)
    stop_event, lost_event, task = heartbeat
    try:
        await asyncio.sleep(0.05)
    finally:
        stop_event.set()
        await task

    assert lost_event.is_set()
    assert queries[0]["worker_id"] == "3"
    assert queries[0]["lease_id"] == "old-generation"


@pytest.mark.asyncio
async def test_expired_lease_rejects_progress_and_telegram_retry_renewal():
    file_doc = {
        "_id": "file-expired",
        "status": "uploading",
        "worker_id": "3",
        "lease_id": "expired-generation",
        "lease_until": int(main.time.time()) - 1,
    }
    queries = []

    class Files:
        async def update_one(self, query, _update):
            queries.append(query)
            return _matched_result(_matches(file_doc, query))

    mongo = SimpleNamespace(files=Files())
    with pytest.raises(main.UploadLeaseLostError, match="no longer matches"):
        await main._update_owned_upload(mongo, file_doc, {"uploaded_bytes": 1})
    with pytest.raises(main.UploadLeaseLostError, match="changed before retry"):
        await main._renew_upload_lease_for_retry(mongo, file_doc)

    assert len(queries) == 2
    assert all(query["lease_until"]["$gt"] > file_doc["lease_until"] for query in queries)


@pytest.mark.asyncio
async def test_parallel_upload_worker_cancels_blocked_readahead_after_upload_failure(monkeypatch, tmp_path):
    media = tmp_path / "large-episode.mkv"
    media.write_bytes(b"x" * 40)
    file_doc = {"_id": "file-fail", "status": "queued", "parts": []}
    updates = []
    producer_tasks = []
    sibling_started = asyncio.Event()
    sibling_cancelled = asyncio.Event()
    upload_tasks = []
    failed_updates_after_siblings_stopped = []

    class Files:
        async def find_one(self, _query):
            return file_doc

        async def find_one_and_update(self, _query, _update, *, return_document):
            _apply_update(file_doc, _update)
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))
            matched = _matches(file_doc, query)
            if matched:
                _apply_update(file_doc, update)
                if update.get("$set", {}).get("status") in {"failed", "retry_wait"}:
                    failed_updates_after_siblings_stopped.append(sibling_cancelled.is_set())
            return _matched_result(matched)

    queue = asyncio.Queue()
    original_producer = main._readahead_producer

    async def track_producer(*args, **kwargs):
        producer_tasks.append(asyncio.current_task())
        await original_producer(*args, **kwargs)

    async def resolve_parent(*_args):
        return "Series"

    async def log_state(*_args):
        return None

    async def fail_upload(_worker_id, _bots, _chat_id, _path, _uuid, part_num, *_args, **_kwargs):
        upload_tasks.append(asyncio.current_task())
        if part_num == 0:
            await asyncio.wait_for(sibling_started.wait(), timeout=1)
            raise RuntimeError("simulated Telegram failure")
        sibling_started.set()
        try:
            await asyncio.Event().wait()
        finally:
            sibling_cancelled.set()

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
        assert sibling_started.is_set()
        assert sibling_cancelled.is_set(), "sibling upload must stop before the failed item is released"
        assert failed_updates_after_siblings_stopped == [True]
        assert all(task.done() for task in upload_tasks)
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
        for task in upload_tasks:
            if not task.done():
                task.cancel()
        if upload_tasks:
            await asyncio.gather(*upload_tasks, return_exceptions=True)


@pytest.mark.asyncio
async def test_small_file_worker_uploads_single_payload_and_persists_completion(monkeypatch, tmp_path):
    result = await _run_small_file_worker(monkeypatch, tmp_path, fail_upload=False)

    assert result["payloads"] == [("S3", b"cover-bytes", 0)]
    final_update = result["updates"][-1][1]
    assert final_update["$set"]["status"] == "completed"
    assert final_update["$set"]["part_count"] == 1
    assert final_update["$set"]["size"] == len(b"cover-bytes")
    assert final_update["$unset"] == {
        "worker_id": 1,
        "lease_id": 1,
        "lease_until": 1,
        "heartbeat_at": 1,
        "uploadId": 1,
        "local_path": 1,
    }
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
            _apply_update(file_doc, _update)
            return file_doc

        async def update_one(self, query, update):
            updates.append((query, update))
            matched = _matches(file_doc, query)
            if matched:
                _apply_update(file_doc, update)
            return _matched_result(matched)

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
