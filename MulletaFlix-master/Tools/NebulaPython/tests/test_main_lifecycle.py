from __future__ import annotations

import asyncio
import importlib

import pytest

main_module = importlib.import_module("main")
stream_service_module = importlib.import_module("stream_service")


@pytest.mark.asyncio
async def test_stream_service_cancels_http_server_when_supervisor_lease_is_lost(monkeypatch):
    started = asyncio.Event()
    cancelled = asyncio.Event()

    async def missing_owner(stop_event, port, token, interval=5):
        assert port == 12345
        assert token == "lease-token"
        await started.wait()
        stop_event.set()

    class Server:
        async def serve_forever(self):
            started.set()
            try:
                await asyncio.Future()
            except asyncio.CancelledError:
                cancelled.set()
                raise

    monkeypatch.setattr(main_module, "_watch_server_owner", missing_owner)
    monkeypatch.setenv("NEBULA_CACHE_LEASE_PORT", "12345")
    monkeypatch.setenv("NEBULA_CACHE_LEASE_TOKEN", "lease-token")

    await stream_service_module._serve_until_owner_stops(Server())

    assert cancelled.is_set()


@pytest.mark.asyncio
async def test_stream_service_cancels_watchdog_when_http_server_stops(monkeypatch):
    watchdog_cancelled = asyncio.Event()

    async def live_owner(stop_event, _port, _token, interval=5):
        try:
            await asyncio.Future()
        except asyncio.CancelledError:
            watchdog_cancelled.set()
            raise

    class Server:
        async def serve_forever(self):
            return

    monkeypatch.setattr(main_module, "_watch_server_owner", live_owner)
    monkeypatch.setenv("NEBULA_CACHE_LEASE_PORT", "12345")
    monkeypatch.setenv("NEBULA_CACHE_LEASE_TOKEN", "lease-token")

    await stream_service_module._serve_until_owner_stops(Server())

    assert watchdog_cancelled.is_set()


@pytest.mark.asyncio
async def test_stream_service_keeps_legacy_behavior_without_supervisor_lease(monkeypatch):
    called = asyncio.Event()

    class Server:
        async def serve_forever(self):
            called.set()

    monkeypatch.delenv("NEBULA_CACHE_LEASE_PORT", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_TOKEN", raising=False)

    await stream_service_module._serve_until_owner_stops(Server())

    assert called.is_set()


@pytest.mark.asyncio
async def test_supervisor_lease_requires_authenticated_live_owner():
    received = []

    async def handle(reader, writer):
        import json

        received.append(json.loads(await reader.readline()))
        writer.write(b'{"ok":true}\n')
        await writer.drain()
        writer.close()
        await writer.wait_closed()

    server = await asyncio.start_server(handle, "127.0.0.1", 0)
    try:
        port = server.sockets[0].getsockname()[1]
        assert await main_module._supervisor_lease_is_alive(port, "test-secret")
        assert received == [{"action": "heartbeat", "token": "test-secret"}]
    finally:
        server.close()
        await server.wait_closed()


@pytest.mark.asyncio
async def test_orphan_watchdog_stops_worker_after_three_failed_heartbeats(monkeypatch):
    calls = 0

    async def owner_missing(_port, _token):
        nonlocal calls
        calls += 1
        return False

    monkeypatch.setattr(main_module, "_supervisor_lease_is_alive", owner_missing)
    stop_event = asyncio.Event()

    await main_module._watch_server_owner(stop_event, 12345, "test-secret", interval=0)

    assert stop_event.is_set()
    assert calls == 3


@pytest.mark.asyncio
async def test_orphan_watchdog_keeps_worker_when_owner_heartbeat_recovers(monkeypatch):
    calls = 0

    async def owner_health(_port, _token):
        nonlocal calls
        calls += 1
        return calls > 1

    monkeypatch.setattr(main_module, "_supervisor_lease_is_alive", owner_health)
    stop_event = asyncio.Event()
    watchdog = asyncio.create_task(
        main_module._watch_server_owner(stop_event, 12345, "test-secret", interval=0)
    )
    try:
        await asyncio.sleep(0.01)
    finally:
        watchdog.cancel()
        with pytest.raises(asyncio.CancelledError):
            await watchdog

    assert calls > 1
    assert not stop_event.is_set()


@pytest.mark.asyncio
async def test_channel_access_checks_run_concurrently_for_all_upload_bots():
    active = 0
    peak_active = 0

    class Bot:
        async def get_chat(self, _target):
            nonlocal active, peak_active
            active += 1
            peak_active = max(peak_active, active)
            await asyncio.sleep(0.01)
            active -= 1

    bots = [Bot() for _ in range(27)]
    failed = await main_module._confirm_upload_bots(bots, "channel", first_index=2)

    assert failed == []
    assert peak_active == 8


@pytest.mark.asyncio
async def test_main_rejects_missing_configuration_before_starting_resources(monkeypatch, tmp_path):
    started = []

    def missing_config():
        raise RuntimeError("MONGODB is required")

    monkeypatch.setattr(main_module, "get_required_config", missing_config)
    monkeypatch.setattr(main_module, "Client", lambda *_args, **_kwargs: started.append("bot"))
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))

    assert await main_module.main() == 1
    assert started == []
    assert not (tmp_path / "sessions").exists()


@pytest.mark.asyncio
async def test_main_stops_started_bots_when_mongo_indexes_fail(monkeypatch, tmp_path):
    events = []

    class Bot:
        async def start(self):
            events.append("bot-start")

        async def stop(self):
            events.append("bot-stop")

    class MotorClient:
        def __init__(self, *_args, **_kwargs):
            pass

        def __getitem__(self, _name):
            return object()

    async def broken_indexes(_database):
        raise RuntimeError("mongo index unavailable")

    monkeypatch.setattr(main_module, "get_required_config", lambda: (123, "hash", ["token"]))
    monkeypatch.setattr(main_module, "Client", lambda *_args, **_kwargs: Bot())
    monkeypatch.setattr(main_module, "AsyncIOMotorClient", MotorClient)
    monkeypatch.setattr(main_module, "resolve_channel", lambda _bot: asyncio.sleep(0, result="channel"))
    monkeypatch.setattr(main_module, "setup_database_indexes", broken_indexes)
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")

    assert await main_module.main() == 1
    assert events == ["bot-start", "bot-stop"]


@pytest.mark.asyncio
async def test_main_stops_started_bots_when_ftp_security_configuration_is_invalid(monkeypatch, tmp_path):
    events = []

    class Bot:
        async def start(self):
            events.append("bot-start")

        async def stop(self):
            events.append("bot-stop")

    class MotorClient:
        def __init__(self, *_args, **_kwargs):
            pass

        def __getitem__(self, _name):
            return object()

    async def setup_indexes(_database):
        return None

    def invalid_security(*_args):
        raise ValueError("invalid FTP TLS configuration")

    monkeypatch.setattr(main_module, "get_required_config", lambda: (123, "hash", ["token"]))
    monkeypatch.setattr(main_module, "Client", lambda *_args, **_kwargs: Bot())
    monkeypatch.setattr(main_module, "AsyncIOMotorClient", MotorClient)
    monkeypatch.setattr(main_module, "resolve_channel", lambda _bot: asyncio.sleep(0, result="channel"))
    monkeypatch.setattr(main_module, "setup_database_indexes", setup_indexes)
    monkeypatch.setattr(main_module, "validate_ftp_security", invalid_security)
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")

    assert await main_module.main() == 1
    assert events == ["bot-start", "bot-stop"]


@pytest.mark.asyncio
async def test_stream_only_lifecycle_starts_services_and_stops_resources(monkeypatch, tmp_path):
    events = []

    class Bot:
        def __init__(self, name, **kwargs):
            self.name = name
            self.options = kwargs

        async def start(self):
            events.append("bot-start")

        async def stop(self):
            events.append("bot-stop")

    class Files:
        def __init__(self):
            self.docs = []

        async def count_documents(self, _query):
            return 0

    class Database:
        def __init__(self):
            self.files = Files()

    database = Database()

    class MotorClient:
        def __init__(self, uri, **kwargs):
            events.append(("mongo-connect", uri, kwargs["w"]))

        def __getitem__(self, name):
            events.append(("mongo-database", name))
            return database

    class FTPServer:
        connections = set()

        def __init__(self, user_manager, path_io, **kwargs):
            events.append(("ftp-construct", kwargs["security_mode"]))

        async def start(self, host, port):
            events.append(("ftp-start", host, port))

        async def serve_forever(self):
            events.append("ftp-serve")

        async def close(self):
            events.append("ftp-close")

    class HTTPServer:
        def close(self):
            events.append("http-close")

        async def wait_closed(self):
            events.append("http-wait-closed")

    async def background(*_args, **_kwargs):
        events.append("background-task")

    async def start_http(_mongo, _bots):
        events.append("http-start")
        return HTTPServer()

    monkeypatch.setattr(main_module, "get_required_config", lambda: (123, "hash", ["token"]))
    monkeypatch.setattr(main_module, "Client", Bot)
    monkeypatch.setattr(main_module, "AsyncIOMotorClient", MotorClient)
    monkeypatch.setattr(main_module, "Server", FTPServer)
    monkeypatch.setattr(main_module, "MongoDBUserManager", lambda _mongo: object())
    monkeypatch.setattr(main_module, "resolve_channel", lambda _bot: asyncio.sleep(0, result="channel"))
    monkeypatch.setattr(main_module, "setup_database_indexes", background)
    monkeypatch.setattr(main_module, "validate_ftp_security", lambda *_args: False)
    monkeypatch.setattr(main_module, "start_http_stream_server", start_http)
    monkeypatch.setattr(main_module, "garbage_collector", background)
    monkeypatch.setattr(main_module, "stats_reporter", background)
    monkeypatch.setattr(main_module, "staging_scanner", background)
    monkeypatch.setattr(main_module, "STREAM_ONLY", True)
    monkeypatch.setattr(main_module, "CONTROL_ENABLED", False)
    monkeypatch.setattr(main_module, "STREAM_HOST", "127.0.0.1")
    monkeypatch.setattr(main_module, "STREAM_PORT", 19091)
    monkeypatch.setattr(main_module, "FTP_SECURITY_MODE", "disabled")
    monkeypatch.setattr(main_module, "TLS_CERTFILE", "")
    monkeypatch.setattr(main_module, "TLS_KEYFILE", "")
    monkeypatch.setattr(main_module, "TLS_REQUIRED", False)
    monkeypatch.setattr(main_module, "TLS_REQUIRE_CLIENT_CERT", False)
    monkeypatch.setattr(main_module, "PASSIVE_PORTS", None)
    monkeypatch.setattr(main_module, "UPLOAD_QUEUE", asyncio.Queue())
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")
    monkeypatch.setenv("HOST", "127.0.0.1")
    monkeypatch.setenv("PORT", "21210")

    result = await main_module.main()

    assert result == 0
    assert (tmp_path / "sessions").is_dir()
    assert "bot-start" in events
    assert ("ftp-start", "127.0.0.1", 21210) in events
    assert "http-start" in events
    assert "ftp-close" in events
    assert "http-close" in events
    assert "http-wait-closed" in events
    assert "bot-stop" in events
    assert events.index("ftp-close") < events.index("bot-stop")


@pytest.mark.asyncio
async def test_main_rejects_ftp_port_collision_and_stops_started_bots(monkeypatch, tmp_path):
    events = []

    class Bot:
        def __init__(self, *_args, **_kwargs):
            pass

        async def start(self):
            events.append("bot-start")

        async def stop(self):
            events.append("bot-stop")

    class MotorClient:
        def __init__(self, *_args, **_kwargs):
            pass

        def __getitem__(self, _name):
            return object()

    class FTPServer:
        def __init__(self, *_args, **_kwargs):
            pass

        async def start(self, host, port):
            events.append(("ftp-start", host, port))
            raise OSError(10048, "address already in use")

    async def setup_indexes(_database):
        return None

    async def start_http(_mongo, _bots):
        events.append("http-start")
        raise AssertionError("HTTP stream must not start after FTP bind failure")

    monkeypatch.setattr(main_module, "get_required_config", lambda: (123, "hash", ["token"]))
    monkeypatch.setattr(main_module, "Client", Bot)
    monkeypatch.setattr(main_module, "AsyncIOMotorClient", MotorClient)
    monkeypatch.setattr(main_module, "Server", FTPServer)
    monkeypatch.setattr(main_module, "MongoDBUserManager", lambda _mongo: object())
    monkeypatch.setattr(main_module, "resolve_channel", lambda _bot: asyncio.sleep(0, result="channel"))
    monkeypatch.setattr(main_module, "setup_database_indexes", setup_indexes)
    monkeypatch.setattr(main_module, "validate_ftp_security", lambda *_args: False)
    monkeypatch.setattr(main_module, "start_http_stream_server", start_http)
    monkeypatch.setattr(main_module, "STREAM_ONLY", False)
    monkeypatch.setattr(main_module, "FTP_SECURITY_MODE", "disabled")
    monkeypatch.setattr(main_module, "TLS_CERTFILE", "")
    monkeypatch.setattr(main_module, "TLS_KEYFILE", "")
    monkeypatch.setattr(main_module, "TLS_REQUIRED", False)
    monkeypatch.setattr(main_module, "TLS_REQUIRE_CLIENT_CERT", False)
    monkeypatch.setattr(main_module, "PASSIVE_PORTS", None)
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")
    monkeypatch.setenv("HOST", "127.0.0.1")
    monkeypatch.setenv("PORT", "21210")

    assert await main_module.main() == 1
    assert ("ftp-start", "127.0.0.1", 21210) in events
    assert "http-start" not in events
    assert events.count("bot-start") == 1
    assert events.count("bot-stop") == 1
