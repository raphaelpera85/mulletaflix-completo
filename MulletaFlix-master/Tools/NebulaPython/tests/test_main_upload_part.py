from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


@pytest.mark.parametrize(
    ("configured_mb", "expected_mb"),
    [(64, 45), (50, 45), (45, 45), (12, 12), (0, 1)],
)
def test_bot_api_chunk_size_is_bounded_with_safe_headroom(configured_mb, expected_mb):
    assert main.normalize_bot_api_chunk_size_mb(configured_mb) == expected_mb


def _message(file_id="telegram-file", message_id=91):
    return SimpleNamespace(document=SimpleNamespace(file_id=file_id), id=message_id)


@pytest.mark.asyncio
async def test_upload_part_sends_bytes_and_returns_resume_metadata(monkeypatch):
    bot = SimpleNamespace(name="bot-a")
    calls = []

    async def send(part_bot, chat_id, payload, chunk_name, *, caption):
        calls.append((part_bot, chat_id, payload, chunk_name, caption))
        return _message()

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=iter([1.0, 2.0, 3.0]).__next__))

    result = await main.upload_part_with_retries(
        worker_id=3,
        bots=[bot],
        target_chat_id=-100,
        local_path="not-opened.mkv",
        file_uuid="uuid",
        part_num=2,
        chunk_data=b"payload",
        bot_index_offset=4,
        caption="title part 2",
    )

    assert calls == [(bot, -100, b"payload", "uuid.part_002", "title part 2")]
    assert result == {
        "part_id": 2,
        "tg_file": "telegram-file",
        "tg_message": 91,
        "file_size": 7,
        "chunk_name": "uuid.part_002",
        "byte_start": 2 * main.CHUNK_SIZE,
        "byte_end": 2 * main.CHUNK_SIZE + 6,
        "caption": "title part 2",
        "bot_index": 4,
        "bot_name": "bot-a",
    }


@pytest.mark.asyncio
async def test_upload_part_reads_requested_chunk_offset_from_local_file(monkeypatch, tmp_path):
    media = tmp_path / "episode.bin"
    media.write_bytes(b"abcdefghij")
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    observed = []

    async def send(_bot, _chat, payload, _name, *, caption):
        observed.append((payload, caption))
        return _message()

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=iter([3.0, 4.0]).__next__))

    result = await main.upload_part_with_retries(
        1, [SimpleNamespace(name="reader")], 7, str(media), "file", 2, caption="resume"
    )

    assert observed == [(b"ij", "resume")]
    assert result["byte_start"] == 8
    assert result["byte_end"] == 9
    assert result["file_size"] == 2


@pytest.mark.asyncio
async def test_upload_part_rotates_bot_after_flood_wait(monkeypatch):
    class SimulatedFloodWait(Exception):
        def __init__(self, value):
            self.value = value

    bots = [SimpleNamespace(name="bot-a"), SimpleNamespace(name="bot-b")]
    calls = []
    monkeypatch.setattr(main, "FloodWait", SimulatedFloodWait)
    monkeypatch.setattr(main, "MAX_RETRIES", 2)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def send(bot, *_args, **_kwargs):
        calls.append(bot)
        if len(calls) == 1:
            raise SimulatedFloodWait(30)
        return _message()

    async def no_wait(_seconds):
        pytest.fail("multiple bots should rotate without sleeping")

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=iter([1.0, 2.0, 3.0]).__next__))

    result = await main.upload_part_with_retries(1, bots, 7, "unused", "file", 0, b"data")

    assert calls == bots
    assert result["bot_index"] == 1
    assert result["bot_name"] == "bot-b"


@pytest.mark.asyncio
async def test_upload_part_rotates_after_transport_timeout_and_retries(monkeypatch):
    bots = [SimpleNamespace(name="bot-a"), SimpleNamespace(name="bot-b")]
    calls = []
    sleeps = []
    monkeypatch.setattr(main, "MAX_RETRIES", 2)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def send(bot, *_args, **_kwargs):
        calls.append(bot)
        if len(calls) == 1:
            raise TimeoutError("socket stalled")
        return _message()

    async def sleep(seconds):
        sleeps.append(seconds)

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", sleep)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=iter([1.0, 2.0, 3.0]).__next__))

    result = await main.upload_part_with_retries(1, bots, 7, "unused", "file", 1, b"data")

    assert calls == bots
    assert sleeps == [1]
    assert result["bot_name"] == "bot-b"


@pytest.mark.asyncio
async def test_upload_part_rejects_empty_payload_and_empty_bot_pool(monkeypatch):
    monkeypatch.setattr(main, "_send_part_on_idle_bot", pytest.fail)

    with pytest.raises(Exception, match="Parte vazia 0"):
        await main.upload_part_with_retries(1, [object()], 7, "unused", "file", 0, b"")
    with pytest.raises(RuntimeError, match="Nenhum bot Telegram disponível"):
        await main.upload_part_with_retries(1, [], 7, "unused", "file", 0, b"payload")


@pytest.mark.asyncio
async def test_upload_part_raises_after_rpc_retries_are_exhausted(monkeypatch):
    class SimulatedRpcError(Exception):
        pass

    sleeps = []
    monkeypatch.setattr(main, "RPCError", SimulatedRpcError)
    monkeypatch.setattr(main, "MAX_RETRIES", 2)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def send(*_args, **_kwargs):
        raise SimulatedRpcError("telegram rejected part")

    async def sleep(seconds):
        sleeps.append(seconds)

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", sleep)

    with pytest.raises(Exception, match="Falha upload parte 4"):
        await main.upload_part_with_retries(2, [SimpleNamespace(name="bot")], 7, "unused", "file", 4, b"data")

    assert sleeps == [2, 4]


@pytest.mark.asyncio
async def test_readahead_producer_queues_chunks_and_end_marker(monkeypatch, tmp_path):
    media = tmp_path / "readahead.bin"
    media.write_bytes(b"abcdefghij")
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    queue = asyncio.Queue()

    await main._readahead_producer(str(media), total_parts=3, queue=queue, worker_id=5)

    assert [await queue.get() for _ in range(4)] == [
        (0, b"abcd"),
        (1, b"efgh"),
        (2, b"ij"),
        None,
    ]


@pytest.mark.asyncio
async def test_readahead_producer_resumes_from_explicit_byte_offset(monkeypatch, tmp_path):
    media = tmp_path / "resumed.bin"
    media.write_bytes(b"abcdefghij")
    monkeypatch.setattr(main, "CHUNK_SIZE", 4)
    queue = asyncio.Queue()

    await main._readahead_producer(
        str(media), total_parts=3, queue=queue, worker_id=2, start_part=1, start_offset=6
    )

    assert [await queue.get() for _ in range(2)] == [(1, b"ghij"), None]
