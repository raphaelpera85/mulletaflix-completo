from __future__ import annotations

import asyncio
import importlib
import uuid
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


def _telegram_history_message(file_uuid, part_num=0, total_parts=1, *, message_id=91, sender_id=500, source_sha256=None):
    digest_marker = f" | SHA256: {source_sha256}" if source_sha256 else ""
    return SimpleNamespace(
        caption=f"[NEBULA] TIPO: series | MIDIA: test | PARTE: {part_num + 1}/{total_parts} | UUID: {file_uuid} | TAM: 1MB{digest_marker}",
        id=message_id,
        document=SimpleNamespace(file_id=f"recovered-file-{message_id}"),
        from_user=SimpleNamespace(id=sender_id),
    )


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

    expected_digest = main.compute_part_sha256(b"payload")
    assert calls == [(bot, -100, b"payload", "uuid.part_002", f"title part 2 | SHA256: {expected_digest}")]
    assert result == {
        "part_id": 2,
        "tg_file": "telegram-file",
        "tg_message": 91,
        "file_size": 7,
        "chunk_name": "uuid.part_002",
        "byte_start": 2 * main.CHUNK_SIZE,
        "byte_end": 2 * main.CHUNK_SIZE + 6,
        "caption": "title part 2 | SHA256: " + main.compute_part_sha256(b"payload"),
        "source_sha256": main.compute_part_sha256(b"payload"),
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

    assert observed == [(b"ij", "resume | SHA256: " + main.compute_part_sha256(b"ij"))]
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

    result = await main.upload_part_with_retries(
        1,
        bots,
        7,
        "unused",
        "file",
        1,
        b"data",
        reconcile=lambda: main.TelegramPartReconciliation(main.TelegramPartReconciliationStatus.NOT_FOUND),
    )

    assert calls == bots
    assert sleeps == [1]
    assert result["bot_name"] == "bot-b"


@pytest.mark.asyncio
async def test_upload_part_reconciles_telegram_acceptance_before_retry(monkeypatch):
    file_uuid = str(uuid.uuid4())
    bot = SimpleNamespace(name="bot-a", me=SimpleNamespace(id=500), history=[])
    sent = []

    async def search_messages(_chat_id, *, query, limit):
        assert query == file_uuid
        assert limit == 50
        for message in bot.history:
            yield message

    bot.search_messages = search_messages

    async def send(_bot, _chat_id, _payload, _name, *, caption):
        sent.append(caption)
        bot.history.append(_telegram_history_message(file_uuid))
        raise ConnectionError("response lost after remote acceptance")

    async def no_wait(_seconds):
        return None

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)
    saved = []

    async def on_success(part):
        saved.append(part)

    result = await main.upload_part_with_retries(
        1,
        [bot],
        -100,
        "unused",
        file_uuid,
        0,
        b"data",
        caption=f"UUID: {file_uuid} | PARTE: 1/1",
        reconcile=lambda: main.reconcile_telegram_part_with_retries([bot], -100, file_uuid, 0, 1),
        on_success=on_success,
    )

    assert len(sent) == 1
    assert result["tg_message"] == 91
    assert result["tg_file"] == "recovered-file-91"
    assert result["bot_index"] == 0
    assert result["bot_name"] == "bot-a"
    assert saved == [result]


@pytest.mark.asyncio
async def test_inconclusive_history_defers_retry_and_later_recovers_without_duplicate(monkeypatch):
    file_uuid = str(uuid.uuid4())
    bot = SimpleNamespace(name="bot-a", me=SimpleNamespace(id=500), history=[])
    sent = []
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def no_wait(_seconds):
        return None

    async def failed_search(*_args, **_kwargs):
        raise OSError("history query failed")

    async def send(_bot, _chat_id, _payload, _name, *, caption):
        sent.append(caption)
        bot.history.append(_telegram_history_message(file_uuid))
        raise ConnectionError("response lost after remote acceptance")

    bot.search_messages = failed_search
    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)
    reconcile = lambda: main.reconcile_telegram_part_with_retries([bot], -100, file_uuid, 0, 1)

    with pytest.raises(main.TelegramReconciliationInconclusiveError, match="retry automático"):
        await main.upload_part_with_retries(
            1, [bot], -100, "unused", file_uuid, 0, b"data",
            caption=f"UUID: {file_uuid} | PARTE: 1/1", reconcile=reconcile,
        )
    assert len(sent) == 1

    bot.search_messages = lambda *_args, **_kwargs: _one_history_message(file_uuid)
    recovered = await main.upload_part_with_retries(
        1, [bot], -100, "unused", file_uuid, 0, b"data",
        caption=f"UUID: {file_uuid} | PARTE: 1/1", reconcile=reconcile,
        reconcile_before_send=True,
    )

    assert len(sent) == 1
    assert recovered["tg_message"] == 91


@pytest.mark.asyncio
async def test_pending_part_is_reconciled_after_restart_without_sending_again(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = main.UploadCheckpointJournal(tmp_path)
    journal.mark_pending(file_uuid, 4, 1234, 1, 4, 0, main.compute_part_sha256(b"data"))
    bot = SimpleNamespace(name="bot-a", me=SimpleNamespace(id=500))
    bot.search_messages = lambda *_args, **_kwargs: _one_history_message(file_uuid)
    monkeypatch.setattr(main, "_send_part_on_idle_bot", lambda *_args, **_kwargs: pytest.fail("duplicate Telegram send"))
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def on_success(part):
        await asyncio.to_thread(journal.save_part, file_uuid, 4, 1234, 1, 4, part)

    recovered = await main.upload_part_with_retries(
        1,
        [bot],
        -100,
        "unused",
        file_uuid,
        0,
        b"data",
        caption=f"UUID: {file_uuid} | PARTE: 1/1",
        reconcile=lambda: main.reconcile_telegram_part_with_retries([bot], -100, file_uuid, 0, 1),
        on_success=on_success,
        reconcile_before_send=True,
    )

    persisted = main.UploadCheckpointJournal(tmp_path).load(file_uuid, 4, 1234, 1, 4)
    assert recovered["tg_message"] == 91
    assert len(persisted.parts) == 1
    assert persisted.pending_parts == frozenset()


@pytest.mark.asyncio
async def test_telegram_reconciliation_requires_exact_part_marker():
    file_uuid = str(uuid.uuid4())

    async def mismatched_part(*_args, **_kwargs):
        yield _telegram_history_message(file_uuid, 0, 10)

    bot = SimpleNamespace(name="bot-a", search_messages=mismatched_part)

    recovered = await main.find_uploaded_telegram_part([bot], -100, file_uuid, 0, 1)

    assert recovered.status is main.TelegramPartReconciliationStatus.NOT_FOUND


@pytest.mark.asyncio
async def test_telegram_reconciliation_requires_exact_content_digest():
    file_uuid = str(uuid.uuid4())
    bot = SimpleNamespace(name="bot-a", search_messages=lambda *_args, **_kwargs: _one_history_message(file_uuid))

    recovered = await main.find_uploaded_telegram_part(
        [bot], -100, file_uuid, 0, 1, source_sha256="a" * 64
    )

    assert recovered.status is main.TelegramPartReconciliationStatus.NOT_FOUND


@pytest.mark.asyncio
async def test_telegram_reconciliation_distinguishes_search_failure_from_absence():
    async def failed_search(*_args, **_kwargs):
        raise OSError("Telegram history unavailable")

    unavailable = await main.find_uploaded_telegram_part(
        [SimpleNamespace(search_messages=failed_search)], -100, "uuid", 0, 1
    )
    unsupported = await main.find_uploaded_telegram_part(
        [SimpleNamespace()], -100, "uuid", 0, 1
    )
    absent = await main.find_uploaded_telegram_part(
        [SimpleNamespace(search_messages=_empty_history)], -100, "uuid", 0, 1
    )

    async def capped_history(*_args, **_kwargs):
        for message_id in range(50):
            yield _telegram_history_message("other-file", message_id=message_id + 1)

    truncated = await main.find_uploaded_telegram_part(
        [SimpleNamespace(search_messages=capped_history)], -100, "uuid", 0, 1
    )

    assert unavailable.status is main.TelegramPartReconciliationStatus.INCONCLUSIVE
    assert unsupported.status is main.TelegramPartReconciliationStatus.INCONCLUSIVE
    assert absent.status is main.TelegramPartReconciliationStatus.NOT_FOUND
    assert truncated.status is main.TelegramPartReconciliationStatus.INCONCLUSIVE


def test_inconclusive_history_retry_preserves_partial_staging_file():
    partial_file = {"parts": [{"tg_message": 91}]}
    retrying_file = {
        **partial_file,
        "failed_reason": "telegram_reconciliation_inconclusive: history unavailable",
    }
    unrelated_failure = {**partial_file, "failed_reason": "disk read failed"}

    assert main.is_telegram_history_retry(retrying_file)
    assert not main.is_telegram_history_retry(unrelated_failure)


async def _one_history_message(file_uuid):
    yield _telegram_history_message(file_uuid)


@pytest.mark.asyncio
async def test_upload_part_keeps_automatic_retry_when_history_does_not_find_message(monkeypatch):
    bot = SimpleNamespace(name="bot-a", search_messages=_empty_history)
    calls = []
    sleeps = []
    monkeypatch.setattr(main, "MAX_RETRIES", 2)
    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)

    async def send(*_args, **_kwargs):
        calls.append(True)
        if len(calls) == 1:
            raise ConnectionError("remote acceptance not observed")
        return _message()

    async def sleep(seconds):
        sleeps.append(seconds)

    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", sleep)
    result = await main.upload_part_with_retries(
        1,
        [bot],
        -100,
        "unused",
        str(uuid.uuid4()),
        0,
        b"data",
        reconcile=lambda: main.reconcile_telegram_part_with_retries([bot], -100, "unknown", 0, 1),
    )

    assert len(calls) == 2
    assert result["tg_message"] == 91
    assert sleeps == [0.25, 0.5, 1, 1]


async def _empty_history(*_args, **_kwargs):
    if False:
        yield None


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
