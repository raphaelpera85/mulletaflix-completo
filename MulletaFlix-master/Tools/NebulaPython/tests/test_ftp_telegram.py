from __future__ import annotations

import asyncio
import hashlib
import importlib
import io
from types import SimpleNamespace

import pytest
import aiohttp

tg = importlib.import_module("ftp.tg")


class AsyncLock:
    async def __aenter__(self):
        return self

    async def __aexit__(self, *_args):
        return None


class FakeSession:
    def __init__(self, *, results=None, fail=None):
        self.results = results or []
        self.fail = fail
        self.requests = []
        self.started = False
        self.stopped = False

    async def start(self):
        self.started = True

    async def stop(self):
        self.stopped = True

    async def invoke(self, request, **kwargs):
        self.requests.append((request, kwargs))
        if self.fail:
            raise self.fail
        return self.results.pop(0) if self.results else True


def _upload_client():
    return SimpleNamespace(
        save_file_semaphore=asyncio.Semaphore(1),
        me=SimpleNamespace(is_premium=False),
        rnd_id=lambda: 12345,
        storage=SimpleNamespace(
            dc_id=_async_value(1),
            auth_key=_async_value(b"auth"),
            test_mode=_async_value(False),
        ),
        loop=asyncio.get_running_loop(),
        executor=None,
    )


def _async_value(value):
    async def get_value():
        return value
    return get_value


@pytest.mark.asyncio
async def test_sequential_save_file_uploads_small_file_and_returns_md5_input(monkeypatch):
    payload = b"telegram-upload"
    session = FakeSession()
    monkeypatch.setattr(tg, "Session", lambda *_args, **_kwargs: session)
    upload = io.BytesIO(payload)
    upload.name = "clip.mp4"

    result = await tg.sequential_save_file(_upload_client(), upload)

    assert isinstance(result, tg.raw.types.InputFile)
    assert result.name == "clip.mp4"
    assert result.parts == 1
    assert result.md5_checksum == hashlib.md5(payload).hexdigest()
    assert session.started and session.stopped
    assert len(session.requests) == 1
    assert isinstance(session.requests[0][0], tg.raw.functions.upload.SaveFilePart)
    assert session.requests[0][1] == {"retries": 0, "timeout": tg.UPLOAD_PART_TIMEOUT}


@pytest.mark.asyncio
async def test_sequential_save_file_propagates_part_rejection_and_stops_session(monkeypatch):
    session = FakeSession(results=[False])
    monkeypatch.setattr(tg, "Session", lambda *_args, **_kwargs: session)

    with pytest.raises(RuntimeError, match="Telegram rejected file part 0"):
        await tg.sequential_save_file(_upload_client(), io.BytesIO(b"payload"))

    assert session.started and session.stopped


@pytest.mark.asyncio
async def test_sequential_save_file_stops_session_after_transport_exception(monkeypatch):
    session = FakeSession(fail=OSError("network disconnected"))
    monkeypatch.setattr(tg, "Session", lambda *_args, **_kwargs: session)

    with pytest.raises(OSError, match="network disconnected"):
        await tg.sequential_save_file(_upload_client(), io.BytesIO(b"payload"))

    assert session.started and session.stopped


@pytest.mark.asyncio
async def test_sequential_save_file_rejects_empty_and_invalid_inputs_without_session(monkeypatch):
    monkeypatch.setattr(tg, "Session", lambda *_args, **_kwargs: pytest.fail("empty input must not start Telegram session"))
    with pytest.raises(ValueError, match="equals to 0 B"):
        await tg.sequential_save_file(_upload_client(), io.BytesIO())
    with pytest.raises(ValueError, match="Invalid file"):
        await tg.sequential_save_file(_upload_client(), object())
    assert await tg.sequential_save_file(_upload_client(), None) is None


@pytest.mark.asyncio
async def test_sequential_save_file_uploads_missing_part_and_returns_none(monkeypatch):
    session = FakeSession()
    monkeypatch.setattr(tg, "Session", lambda *_args, **_kwargs: session)

    result = await tg.sequential_save_file(_upload_client(), io.BytesIO(b"part"), file_id=99, file_part=0)

    assert result is None
    request = session.requests[0][0]
    assert isinstance(request, tg.raw.functions.upload.SaveFilePart)
    assert request.file_id == 99
    assert request.file_part == 0
    assert session.stopped


@pytest.mark.asyncio
async def test_send_document_bot_api_requires_token_before_network():
    with pytest.raises(ConnectionError, match="token unavailable"):
        await tg.send_document_bot_api(SimpleNamespace(), 1, b"data", "file.bin")


@pytest.mark.asyncio
async def test_send_document_bot_api_maps_success_payload(monkeypatch):
    observed = {}

    class Response:
        status = 200

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        async def json(self, **kwargs):
            observed["json_kwargs"] = kwargs
            return {"ok": True, "result": {"message_id": 42, "document": {"file_id": "tg-file"}}}

    class ClientSession:
        def __init__(self, **kwargs):
            observed["timeout"] = kwargs["timeout"]

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        def post(self, url, data):
            observed["url"] = url
            observed["form"] = data
            return Response()

    monkeypatch.setattr(tg.aiohttp, "ClientSession", ClientSession)
    bot = SimpleNamespace(_nebula_bot_token="secret")

    result = await tg.send_document_bot_api(bot, -100, b"payload", "movie.mkv", "caption")

    assert result.id == 42
    assert result.document.file_id == "tg-file"
    assert observed["url"] == "https://api.telegram.org/botsecret/sendDocument"
    assert isinstance(observed["timeout"], aiohttp.ClientTimeout)
    assert observed["form"].is_multipart


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("payload", "status", "message"),
    [
        ({"ok": False, "error_code": 429, "description": "too many requests"}, 429, "rejected upload"),
        ({"ok": True, "result": {"message_id": 42, "document": {}}}, 200, "incomplete upload metadata"),
    ],
)
async def test_send_document_bot_api_rejects_bad_response_metadata(monkeypatch, payload, status, message):
    class Response:
        def __init__(self):
            self.status = status

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        async def json(self, **_kwargs):
            return payload

    class ClientSession:
        def __init__(self, **_kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        def post(self, *_args, **_kwargs):
            return Response()

    monkeypatch.setattr(tg.aiohttp, "ClientSession", ClientSession)
    with pytest.raises(ConnectionError, match=message):
        await tg.send_document_bot_api(SimpleNamespace(_nebula_bot_token="x"), 1, b"data", "file.bin")


@pytest.mark.asyncio
async def test_send_document_bot_api_wraps_transport_failure(monkeypatch):
    class ClientSession:
        def __init__(self, **_kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        def post(self, *_args, **_kwargs):
            raise aiohttp.ClientConnectionError("offline")

    monkeypatch.setattr(tg.aiohttp, "ClientSession", ClientSession)
    with pytest.raises(ConnectionError, match="transport failed: ClientConnectionError"):
        await tg.send_document_bot_api(SimpleNamespace(_nebula_bot_token="x"), 1, b"data", "file.bin")


@pytest.mark.parametrize(("offset", "expected"), [(0, 1024 * 1024), (5, 1024 * 1024 - 5), (1024 * 1024, 1024 * 1024), (1024 * 1024 + 3, 1024 * 1024 - 3)])
def test_get_file_limit_aligns_telegram_chunks(offset, expected):
    assert tg.get_file_limit(offset) == expected


@pytest.mark.asyncio
async def test_file_with_unusable_reference_returns_empty_when_no_message_context():
    file = tg.File("not-a-pyrogram-file-id", SimpleNamespace())
    assert await file.getChunkAt(4096) == b""


@pytest.mark.asyncio
@pytest.mark.parametrize("message", [None, SimpleNamespace(document=None)])
async def test_file_returns_empty_when_unusable_reference_cannot_be_refreshed(message):
    async def get_messages(_chat_id, _message_id):
        return message

    client = SimpleNamespace(get_messages=get_messages)
    file = tg.File("unusable", client, chat_id="-100", message_id=42)

    assert await file.getChunkAt(0) == b""
    assert file.reference_refreshed is False


@pytest.mark.asyncio
async def test_file_logs_and_returns_empty_when_unusable_reference_lookup_fails(caplog):
    async def get_messages(_chat_id, _message_id):
        raise OSError("Telegram unavailable")

    file = tg.File(
        "unusable",
        SimpleNamespace(get_messages=get_messages),
        chat_id="-100",
        message_id=42,
    )

    assert await file.getChunkAt(0) == b""
    assert "Erro ao auto-recuperar mensagem 42" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("message", [None, SimpleNamespace(document=None)])
async def test_file_returns_empty_when_expired_reference_message_is_missing(monkeypatch, message):
    async def get_messages(_chat_id, _message_id):
        return message

    class Session:
        async def send(self, _request):
            raise tg.FileReferenceExpired(400, "FILE_REFERENCE_EXPIRED")

    client = SimpleNamespace(get_messages=get_messages)
    file = tg.File("valid", client, chat_id="-100", message_id=42)
    file.id = SimpleNamespace(dc_id=4)
    file.loc = "old-location"

    async def get_session(_client, _file_id):
        return Session()

    monkeypatch.setattr(tg, "get_media_session", get_session)

    assert await file.getChunkAt(0) == b""
    assert file.reference_refreshed is False


@pytest.mark.asyncio
async def test_file_recovers_unusable_reference_from_message_context(monkeypatch):
    calls = []
    client = SimpleNamespace(
        get_messages=lambda chat_id, message_id: _async_value(
            SimpleNamespace(document=SimpleNamespace(file_id="refreshed-file-id"))
        )()
    )
    session = SimpleNamespace(send=lambda request: _async_value(SimpleNamespace(bytes=b"restored chunk"))())
    file = tg.File("unusable", client, chat_id="-100", message_id=42)
    file._set_id = lambda value: (
        setattr(file, "id", SimpleNamespace(dc_id=3)),
        setattr(file, "loc", f"location:{value}"),
    )

    async def get_session(_client, file_id):
        calls.append(file_id)
        return session

    monkeypatch.setattr(tg, "get_media_session", get_session)

    assert await file.getChunkAt(8192) == b"restored chunk"
    assert client.get_messages is not None
    assert file.reference_refreshed is True
    assert file.loc == "location:refreshed-file-id"
    assert calls == [file.id]


@pytest.mark.asyncio
async def test_file_refreshes_reference_after_file_reference_expired(monkeypatch):
    message_queries = []

    class Client:
        async def get_messages(self, chat_id, message_id):
            message_queries.append((chat_id, message_id))
            return SimpleNamespace(document=SimpleNamespace(file_id="new-file-id"))

    class Session:
        def __init__(self):
            self.requests = []

        async def send(self, request):
            self.requests.append(request)
            if len(self.requests) == 1:
                raise tg.FileReferenceExpired(400, "FILE_REFERENCE_EXPIRED")
            return SimpleNamespace(bytes=b"new reference bytes")

    file = tg.File("old-id", Client(), chat_id="-100", message_id=73)
    file.id = SimpleNamespace(dc_id=4)
    file.loc = "old-location"
    file._set_id = lambda value: (
        setattr(file, "id", SimpleNamespace(dc_id=4)),
        setattr(file, "loc", f"location:{value}"),
    )
    session = Session()

    async def get_session(_client, _file_id):
        return session

    monkeypatch.setattr(tg, "get_media_session", get_session)

    result = await file.getChunkAt(4096)

    assert result == b"new reference bytes"
    assert message_queries == [(-100, 73)]
    assert [request.location for request in session.requests] == [
        "old-location", "location:new-file-id"
    ]
    assert all(request.offset == 4096 for request in session.requests)
    assert file.reference_refreshed is True


@pytest.mark.asyncio
async def test_file_retries_timeout_at_same_offset(monkeypatch):
    class Session:
        def __init__(self):
            self.requests = []

        async def send(self, request):
            self.requests.append(request)
            if len(self.requests) == 1:
                raise TimeoutError("temporary Telegram timeout")
            return SimpleNamespace(bytes=b"retried chunk")

    file = tg.File("valid", SimpleNamespace())
    file.id = SimpleNamespace(dc_id=2)
    file.loc = "location"
    session = Session()
    sleeps = []

    async def get_session(_client, _file_id):
        return session

    async def no_wait(seconds):
        sleeps.append(seconds)

    monkeypatch.setattr(tg, "get_media_session", get_session)
    monkeypatch.setattr(tg, "asleep", no_wait)

    assert await file.getChunkAt(1234) == b"retried chunk"
    assert len(session.requests) == 2
    assert all(request.offset == 1234 for request in session.requests)
    assert sleeps == [1]


@pytest.mark.asyncio
@pytest.mark.parametrize("error_type", [tg.OffsetInvalid, tg.LimitInvalid])
async def test_file_returns_empty_for_invalid_telegram_range(monkeypatch, error_type):
    class Session:
        async def send(self, _request):
            raise error_type(400, "INVALID_RANGE")

    file = tg.File("valid", SimpleNamespace())
    file.id = SimpleNamespace(dc_id=2)
    file.loc = "location"

    async def get_session(_client, _file_id):
        return Session()

    monkeypatch.setattr(tg, "get_media_session", get_session)

    assert await file.getChunkAt(1024) == b""


@pytest.mark.asyncio
async def test_get_media_session_creates_and_authenticates_cross_dc_session(monkeypatch):
    events = []

    class Auth:
        def __init__(self, *_args):
            pass

        async def create(self):
            events.append("create-auth")
            return b"exported-auth"

    class Session:
        def __init__(self, client, dc_id, auth_key, test_mode, *, is_media):
            events.append(("construct", dc_id, auth_key, test_mode, is_media))

        async def start(self):
            events.append("start")

        async def invoke(self, request):
            events.append(("import", request.id, request.bytes))

        async def stop(self):
            events.append("stop")

    client = SimpleNamespace(
        media_sessions={},
        media_sessions_lock=AsyncLock(),
        storage=SimpleNamespace(
            dc_id=_async_value(1),
            auth_key=_async_value(b"local-auth"),
            test_mode=_async_value(False),
        ),
        invoke=lambda _request: _async_value(SimpleNamespace(id=8, bytes=b"authorization"))(),
    )
    monkeypatch.setattr(tg, "Auth", Auth)
    monkeypatch.setattr(tg, "Session", Session)

    result = await tg.get_media_session(client, SimpleNamespace(dc_id=4))

    assert result is not None
    assert client.media_sessions[4] is result
    assert events == [
        "create-auth",
        ("construct", 4, b"exported-auth", False, True),
        "start",
        ("import", 8, b"authorization"),
    ]


@pytest.mark.asyncio
async def test_get_media_session_stops_uncached_session_after_auth_retries_are_exhausted(monkeypatch):
    attempts = {"export": 0, "import": 0}
    session_state = {"started": False, "stopped": False}

    class Auth:
        def __init__(self, *_args):
            pass

        async def create(self):
            return b"temporary-auth"

    class Session:
        def __init__(self, *_args, **_kwargs):
            pass

        async def start(self):
            session_state["started"] = True

        async def invoke(self, _request):
            attempts["import"] += 1
            raise tg.AuthBytesInvalid(400, "AUTH_BYTES_INVALID")

        async def stop(self):
            session_state["stopped"] = True

    async def export_authorization(_request):
        attempts["export"] += 1
        return SimpleNamespace(id=8, bytes=b"authorization")

    client = SimpleNamespace(
        media_sessions={},
        media_sessions_lock=AsyncLock(),
        storage=SimpleNamespace(
            dc_id=_async_value(1),
            auth_key=_async_value(b"local-auth"),
            test_mode=_async_value(False),
        ),
        invoke=export_authorization,
    )
    monkeypatch.setattr(tg, "Auth", Auth)
    monkeypatch.setattr(tg, "Session", Session)

    with pytest.raises(tg.AuthBytesInvalid):
        await tg.get_media_session(client, SimpleNamespace(dc_id=4))

    assert attempts == {"export": 6, "import": 6}
    assert session_state == {"started": True, "stopped": True}
    assert client.media_sessions == {}


@pytest.mark.asyncio
async def test_file_stream_respects_requested_offset(monkeypatch):
    file = tg.File("invalid", SimpleNamespace())
    calls = []

    async def fake_chunk(offset=0, refreshed=False):
        calls.append(offset)
        return b"0123456789" if len(calls) == 1 else b""

    monkeypatch.setattr(file, "getChunkAt", fake_chunk)
    chunks = [chunk async for chunk in file.stream(4)]
    assert chunks == [b"456789"]
    assert calls == [0, 10]


@pytest.mark.asyncio
async def test_get_media_session_returns_cached_session_without_reauthentication():
    session = object()
    client = SimpleNamespace(media_sessions={2: session}, media_sessions_lock=AsyncLock())
    file_id = SimpleNamespace(dc_id=2)

    assert await tg.get_media_session(client, file_id) is session
