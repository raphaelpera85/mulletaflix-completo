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
async def test_mtproto_retry_uses_same_random_id_and_fetches_message(monkeypatch):
    calls = []

    class RawNamespace:
        class functions:
            class messages:
                @staticmethod
                def SendMedia(**kwargs):
                    calls.append(kwargs)
                    return SimpleNamespace(**kwargs)

        class types:
            @staticmethod
            def InputMediaUploadedDocument(**kwargs):
                return SimpleNamespace(**kwargs)

            @staticmethod
            def DocumentAttributeFilename(**kwargs):
                return SimpleNamespace(**kwargs)

    raw = RawNamespace()
    monkeypatch.setattr(main, "raw", raw)
    message = _message()

    class Bot:
        def __init__(self):
            self.saved = []

        async def save_file(self, document, **kwargs):
            self.saved.append((document.name, kwargs))
            return SimpleNamespace(id=77)

        def guess_mime_type(self, _name):
            return "video/x-matroska"

        async def resolve_peer(self, chat):
            return f"peer:{chat}"

        async def invoke(self, request):
            return SimpleNamespace(updates=[SimpleNamespace(id=91, random_id=request.random_id)])

        async def get_messages(self, chat, message_ids):
            assert (chat, message_ids) == (-100, 91)
            return message

    bot = Bot()
    result = await main._send_part_document_mtproto(bot, -100, b"payload", "uuid.part_000", "caption", 123456)

    assert result is message
    assert calls[0]["random_id"] == 123456
    assert calls[0]["peer"] == "peer:-100"
    assert bot.saved == [("uuid.part_000", {})]


@pytest.mark.asyncio
async def test_mtproto_missing_part_retries_are_bounded(monkeypatch):
    class RawNamespace:
        class functions:
            class messages:
                @staticmethod
                def SendMedia(**kwargs):
                    return SimpleNamespace(**kwargs)

        class types:
            @staticmethod
            def InputMediaUploadedDocument(**kwargs):
                return SimpleNamespace(**kwargs)

            @staticmethod
            def DocumentAttributeFilename(**kwargs):
                return SimpleNamespace(**kwargs)

    monkeypatch.setattr(main, "raw", RawNamespace())

    class Bot:
        def __init__(self):
            self.invoke_calls = 0
            self.repaired_parts = []

        async def save_file(self, document, **kwargs):
            if kwargs:
                self.repaired_parts.append(kwargs["file_part"])
            return SimpleNamespace(id=77)

        def guess_mime_type(self, _name):
            return "application/octet-stream"

        async def resolve_peer(self, _chat):
            return SimpleNamespace()

        async def invoke(self, _request):
            self.invoke_calls += 1
            raise main.FilePartMissing(value=0)

    bot = Bot()
    with pytest.raises(RuntimeError, match="continuou rejeitando"):
        await main._send_part_document_mtproto(bot, -100, b"x", "part", "caption", 123)

    assert bot.invoke_calls == 2
    assert bot.repaired_parts == [0]


@pytest.mark.asyncio
async def test_persisted_part_retry_pins_bot_and_checkpoints_before_return(monkeypatch):
    bots = [SimpleNamespace(name="bot-a"), SimpleNamespace(name="bot-b")]
    reservation = {"random_id": 987, "bot_id": 44, "bot_name": "bot-b"}
    sends = []
    updates = []
    monkeypatch.setattr(main, "MAX_RETRIES", 2)

    async def reserve(*_args):
        return reservation, 1

    async def send(bot, _chat, _payload, _name, **kwargs):
        sends.append((bot, kwargs.get("random_id")))
        if len(sends) == 1:
            raise TimeoutError("lost response")
        return _message()

    async def no_wait(_seconds):
        pass

    class Files:
        async def update_one(self, query, update):
            updates.append((query, update))
            return SimpleNamespace(matched_count=1)

    mongo = SimpleNamespace(files=Files())
    monkeypatch.setattr(main, "reserve_part_send", reserve)
    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=iter([1.0, 2.0, 3.0]).__next__))

    result = await main.upload_part_with_retries(
        1, bots, -100, "media.mkv", "uuid", 0, b"payload", caption="caption",
        mongo=mongo, file_doc_id="file-id",
    )

    assert sends == [(bots[1], 987), (bots[1], 987)]
    assert result["bot_id"] == 44
    assert updates == [(
        {"_id": "file-id"},
        {"$addToSet": {"parts": result}, "$unset": {"pending_parts.0": ""}},
    )]


@pytest.mark.asyncio
async def test_part_reservation_survives_worker_restart_with_same_random_id(monkeypatch):
    class Files:
        def __init__(self):
            self.doc = {"_id": "file-id", "status": "uploading"}

        async def find_one(self, _query):
            return self.doc

        async def update_one(self, query, update):
            key = "pending_parts.0"
            if query.get(key) == {"$exists": False}:
                self.doc.setdefault("pending_parts", {})["0"] = update["$set"][key]
                return SimpleNamespace(matched_count=1)
            return SimpleNamespace(matched_count=0)

    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    bot = SimpleNamespace(name="bot-a", me=SimpleNamespace(id=44))
    mongo = SimpleNamespace(files=Files())

    first, index = await main.reserve_part_send(
        mongo, "file-id", [bot], -100, "uuid", 0, b"payload", "uuid.part_000", "caption"
    )
    second, second_index = await main.reserve_part_send(
        mongo, "file-id", [bot], -100, "uuid", 0, b"payload", "uuid.part_000", "caption"
    )

    assert index == second_index == 0
    assert first == second
    assert 0 < first["random_id"] < 2**63
    assert first["bot_id"] == 44
    assert len(mongo.files.doc["pending_parts"]) == 1


@pytest.mark.asyncio
async def test_worker_restart_reuses_random_id_after_telegram_accepts_but_response_is_lost(monkeypatch):
    bot = SimpleNamespace(name="bot-a", me=SimpleNamespace(id=44))
    sent_messages = {}
    send_attempts = []

    class Files:
        def __init__(self):
            self.doc = {"_id": "file-id", "status": "uploading"}

        async def find_one(self, _query):
            return self.doc

        async def update_one(self, query, update):
            reservation_key = "pending_parts.0"
            if query.get(reservation_key) == {"$exists": False}:
                if reservation_key in self.doc.get("pending_parts", {}):
                    return SimpleNamespace(matched_count=0)
                self.doc.setdefault("pending_parts", {})["0"] = update["$set"][reservation_key]
                return SimpleNamespace(matched_count=1)

            part = update.get("$addToSet", {}).get("parts")
            if part is not None and part not in self.doc.setdefault("parts", []):
                self.doc["parts"].append(part)
            for key in update.get("$unset", {}):
                self.doc.get("pending_parts", {}).pop(key.removeprefix("pending_parts."), None)
            return SimpleNamespace(matched_count=1)

    async def send(_bot, _chat, _payload, _name, *, random_id, caption):
        send_attempts.append((random_id, caption))
        if random_id not in sent_messages:
            sent_messages[random_id] = _message()
            raise TimeoutError("Telegram accepted the media, response lost")
        return sent_messages[random_id]

    async def no_wait(_seconds):
        return None

    monkeypatch.setattr(main, "MAX_RETRIES", 1)
    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)
    monkeypatch.setattr(main.asyncio, "sleep", no_wait)
    monkeypatch.setattr(main, "time", SimpleNamespace(monotonic=lambda: 1.0))
    mongo = SimpleNamespace(files=Files())

    with pytest.raises(Exception, match="Falha upload parte 0"):
        await main.upload_part_with_retries(
            1, [bot], -100, "media.mkv", "uuid", 0, b"payload", caption="caption",
            mongo=mongo, file_doc_id="file-id",
        )

    reserved_random_id = mongo.files.doc["pending_parts"]["0"]["random_id"]
    result = await main.upload_part_with_retries(
        1, [bot], -100, "media.mkv", "uuid", 0, b"payload", caption="caption",
        mongo=mongo, file_doc_id="file-id",
    )

    assert send_attempts == [(reserved_random_id, "caption"), (reserved_random_id, "caption")]
    assert len(sent_messages) == 1
    assert result["tg_message"] == sent_messages[reserved_random_id].id
    assert mongo.files.doc["parts"] == [result]
    assert mongo.files.doc["pending_parts"] == {}


@pytest.mark.asyncio
async def test_part_reservation_mismatch_blocks_retry_and_preserves_source(monkeypatch):
    class Files:
        def __init__(self):
            self.doc = {
                "_id": "file-id", "status": "uploading", "local_path": "source.mkv",
                "pending_parts": {"0": {
                    "upload_uuid": "uuid", "random_id": 123, "bot_id": 44,
                    "sha256": "old-digest", "file_size": 7, "chunk_name": "uuid.part_000",
                    "caption": "caption", "target_chat_id": "-100",
                }},
            }

        async def find_one(self, _query):
            return self.doc

        async def update_one(self, _query, update):
            self.doc.update(update["$set"])

    mongo = SimpleNamespace(files=Files())
    with pytest.raises(RuntimeError, match="recuperação bloqueada"):
        await main.reserve_part_send(
            mongo, "file-id", [SimpleNamespace(me=SimpleNamespace(id=44))], -100,
            "uuid", 0, b"payload", "uuid.part_000", "caption",
        )

    assert mongo.files.doc["recovery_blocked"] is True
    assert mongo.files.doc["local_path"] == "source.mkv"


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
