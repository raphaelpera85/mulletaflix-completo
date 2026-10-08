from __future__ import annotations

import asyncio
from asyncio import IncompleteReadError

import pytest

from ftp import common


class FakeReader:
    def __init__(self, *, line=b"line\n", data=b"data", exact=None):
        self.line = line
        self.data = data
        self.exact = exact
        self.read_calls = []

    async def readline(self):
        return self.line

    async def read(self, count=-1):
        self.read_calls.append(count)
        if count < 0:
            result, self.data = self.data, b""
        else:
            result, self.data = self.data[:count], self.data[count:]
        return result

    async def readexactly(self, count):
        if isinstance(self.exact, BaseException):
            raise self.exact
        return self.exact if self.exact is not None else b"x" * count


class FakeWriter:
    def __init__(self):
        self.writes = []
        self.drain_calls = 0
        self.closed = False

    def write(self, data):
        self.writes.append(data)

    async def drain(self):
        self.drain_calls += 1

    def close(self):
        self.closed = True


@pytest.mark.asyncio
async def test_stream_io_forwards_reads_writes_and_close():
    reader = FakeReader()
    writer = FakeWriter()
    stream = common.StreamIO(reader, writer)

    assert await stream.readline() == b"line\n"
    assert await stream.read(2) == b"da"
    assert await stream.read() == b"ta"
    assert await stream.readexactly(3) == b"xxx"
    await stream.write(b"payload")
    stream.close()

    assert reader.read_calls == [2, -1]
    assert writer.writes == [b"payload"]
    assert writer.drain_calls == 1
    assert writer.closed


@pytest.mark.asyncio
async def test_stream_io_returns_partial_data_on_incomplete_read():
    partial = IncompleteReadError(b"partial", expected=8)
    stream = common.StreamIO(FakeReader(exact=partial), FakeWriter())

    assert await stream.readexactly(8) == b"partial"


@pytest.mark.asyncio
async def test_stream_io_context_manager_closes_writer_on_exit():
    writer = FakeWriter()
    stream = common.StreamIO(FakeReader(), writer)

    assert await stream.__aenter__() is None
    await stream.__aexit__(None, None, None)

    assert writer.closed


@pytest.mark.asyncio
async def test_stream_io_iterates_blocks_and_keeps_final_partial_block():
    class ChunkReader:
        def __init__(self):
            self.chunks = [b"abcd", b"xy"]

        async def readexactly(self, _count):
            if not self.chunks:
                raise IncompleteReadError(b"", expected=4)
            chunk = self.chunks.pop(0)
            if len(chunk) < 4:
                raise IncompleteReadError(chunk, expected=4)
            return chunk

    stream = common.StreamIO(ChunkReader(), FakeWriter())

    assert [chunk async for chunk in stream.iter_by_block(4)] == [b"abcd", b"xy"]


@pytest.mark.asyncio
async def test_abstract_async_lister_supports_iteration_and_await():
    class Lister(common.AbstractAsyncLister):
        def __init__(self, values):
            self.values = iter(values)

        async def __anext__(self):
            try:
                return next(self.values)
            except StopIteration:
                raise StopAsyncIteration

    assert [value async for value in Lister([1, 2])] == [1, 2]
    assert await Lister(["a", "b"]) == ["a", "b"]


@pytest.mark.parametrize(
    ("value", "expected"),
    [("media", ("media",)), [("media", "sidecar"), ("media", "sidecar")]],
)
def test_wrap_with_container(value, expected):
    assert common.wrap_with_container(value) == expected


def test_setlocale_restores_previous_locale_after_success_or_failure(monkeypatch):
    calls = []

    def fake_setlocale(_category, value=None):
        calls.append(value)
        return "old-locale" if value is None else value

    monkeypatch.setattr(common, "_setlocale", fake_setlocale)

    with common.setlocale("requested-locale") as selected:
        assert selected == "requested-locale"
    assert calls == [None, "requested-locale", "old-locale"]

    calls.clear()
    with pytest.raises(RuntimeError, match="operation failed"):
        with common.setlocale("temporary-locale"):
            raise RuntimeError("operation failed")
    assert calls == [None, "temporary-locale", "old-locale"]


@pytest.mark.asyncio
async def test_upload_queue_waiter_wakes_when_a_small_item_arrives():
    queue = common.DualLaneUploadQueue(small_file_max_bytes=4)
    waiter = asyncio.create_task(queue.get())
    await asyncio.sleep(0)

    await queue.put({"name": "cover.jpg", "size": 4})

    assert await asyncio.wait_for(waiter, timeout=1) == {"name": "cover.jpg", "size": 4}
    assert queue.empty()
    queue.task_done()
    await queue.join()


def test_upload_queue_treats_missing_file_stat_as_zero_bytes(monkeypatch):
    def fail_getsize(_path):
        raise OSError("cannot stat")

    monkeypatch.setattr(common.os.path, "exists", lambda _path: True)
    monkeypatch.setattr(common.os.path, "getsize", fail_getsize)
    queue = common.DualLaneUploadQueue(small_file_max_bytes=0)

    assert queue.is_small({"path": "unavailable.mkv"})
