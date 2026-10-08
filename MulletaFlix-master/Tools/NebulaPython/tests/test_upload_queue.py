from __future__ import annotations

import asyncio
import os

import pytest

from ftp.common import DualLaneUploadQueue


@pytest.mark.asyncio
async def test_get_prioritizes_small_files_and_tracks_lane_sizes():
    queue = DualLaneUploadQueue(small_file_max_bytes=10)
    small = {"name": "small", "size": 10}
    large = {"name": "large", "size": 11}
    await queue.put(large)
    await queue.put(small)

    assert queue.qsize() == 2
    assert queue.small_qsize() == 1
    assert queue.large_qsize() == 1
    assert await queue.get() == small
    assert await queue.get() == large
    assert queue.empty()


@pytest.mark.asyncio
async def test_get_large_falls_back_to_small_only_when_configured():
    queue = DualLaneUploadQueue(small_file_max_bytes=10)
    small = {"name": "small", "size": 1}
    await queue.put(small)

    assert await queue.get_large(fallback_to_small=True) == small


@pytest.mark.asyncio
async def test_get_large_waits_for_large_item_without_consuming_small_item():
    queue = DualLaneUploadQueue(small_file_max_bytes=10)
    small = {"name": "small", "size": 1}
    large = {"name": "large", "size": 11}
    await queue.put(small)
    task = asyncio.create_task(queue.get_large(fallback_to_small=False))
    await asyncio.sleep(0)
    assert not task.done()

    await queue.put(large)
    assert await task == large
    assert queue.get_nowait() == small


@pytest.mark.asyncio
async def test_join_waits_until_all_items_are_acknowledged():
    queue = DualLaneUploadQueue(small_file_max_bytes=10)
    await queue.put({"size": 1})
    waiter = asyncio.create_task(queue.join())
    await asyncio.sleep(0)
    assert not waiter.done()

    await queue.get()
    queue.task_done()
    await waiter
    assert queue.empty()


def test_put_nowait_and_get_nowait_use_small_lane_first():
    queue = DualLaneUploadQueue(small_file_max_bytes=10)
    large = {"size": 11}
    small = {"size": 2}
    queue.put_nowait(large)
    queue.put_nowait(small)

    assert queue.get_nowait() == small
    assert queue.get_nowait() == large


def test_task_done_rejects_more_acknowledgements_than_enqueued_items():
    queue = DualLaneUploadQueue()
    with pytest.raises(ValueError, match="too many times"):
        queue.task_done()


def test_is_small_uses_existing_file_size_and_treats_invalid_items_as_large(tmp_path, monkeypatch):
    path = tmp_path / "sample.bin"
    path.write_bytes(b"12345")
    queue = DualLaneUploadQueue(small_file_max_bytes=4)
    monkeypatch.setattr(os.path, "getsize", lambda _path: 5)

    assert not queue.is_small({"path": str(path)})
    assert not queue.is_small(None)

