from __future__ import annotations

import asyncio
import importlib

import pytest

stream_service = importlib.import_module("stream_service")


class FakeBot:
    def __init__(self, name, **options):
        self.name = name
        self.options = options
        self.started = False
        self.stopped = False
        self._nebula_bot_token = None

    async def start(self):
        if self.name.endswith("2"):
            raise RuntimeError("bot authentication failed")
        self.started = True

    async def stop(self):
        self.stopped = True


class FakeDatabase:
    def __init__(self, *, ping_error=None):
        self.ping_error = ping_error

    async def command(self, command):
        assert command == "ping"
        if self.ping_error:
            raise self.ping_error
        return {"ok": 1}


class FakeMongoClient:
    def __init__(self, _uri, **options):
        self.options = options
        self.database = FakeDatabase()
        self.closed = False

    def __getitem__(self, _name):
        return self.database

    def close(self):
        self.closed = True


class FakeServer:
    def __init__(self):
        self.closed = False
        self.waited_closed = False

    async def serve_forever(self):
        raise asyncio.CancelledError

    def close(self):
        self.closed = True

    async def wait_closed(self):
        self.waited_closed = True


@pytest.mark.asyncio
async def test_run_returns_failure_before_starting_services_when_config_is_invalid(monkeypatch):
    def invalid_config():
        raise RuntimeError("missing config")

    monkeypatch.setattr(stream_service.nebula, "get_required_config", invalid_config)
    monkeypatch.setattr(stream_service, "Client", lambda *_args, **_kwargs: pytest.fail("bot should not start"))

    assert await stream_service.run() == 1


@pytest.mark.asyncio
async def test_run_stops_all_resources_when_stream_server_is_cancelled(monkeypatch, tmp_path):
    bots = []
    mongo_client = FakeMongoClient("mongodb://test")
    server = FakeServer()

    monkeypatch.setattr(stream_service.nebula, "get_required_config", lambda: (123, "hash", ["ok", "bad"]))
    monkeypatch.setattr(stream_service, "Client", lambda name, **options: bots.append(FakeBot(name, **options)) or bots[-1])
    monkeypatch.setattr(stream_service, "AsyncIOMotorClient", lambda uri, **options: mongo_client)
    monkeypatch.setattr(stream_service.nebula, "start_http_stream_server", _async_return(server))
    monkeypatch.setenv("MONGODB", "mongodb://test")
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))

    with pytest.raises(asyncio.CancelledError):
        await stream_service.run()

    assert len(bots) == 2
    assert all(bot.options["workdir"] == str(tmp_path / "sessions") for bot in bots)
    assert [bot._nebula_bot_token for bot in bots] == ["ok", "bad"]
    assert bots[0].started and bots[0].stopped
    assert not bots[1].started and bots[1].stopped
    assert (tmp_path / "sessions").is_dir()
    assert mongo_client.closed
    assert server.closed and server.waited_closed


@pytest.mark.asyncio
async def test_run_returns_failure_and_stops_clients_when_no_bot_authenticates(monkeypatch, tmp_path):
    bots = []

    async def fail_start(_self):
        raise RuntimeError("invalid token")

    monkeypatch.setattr(stream_service.nebula, "get_required_config", lambda: (123, "hash", ["bad"]))
    monkeypatch.setattr(stream_service, "Client", lambda name, **options: bots.append(FakeBot(name, **options)) or bots[-1])
    monkeypatch.setattr(FakeBot, "start", fail_start)
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))

    assert await stream_service.run() == 1
    assert len(bots) == 1
    assert bots[0].stopped


@pytest.mark.asyncio
async def test_run_closes_mongo_and_authenticated_bots_when_database_ping_fails(monkeypatch, tmp_path):
    bots = []
    mongo_client = FakeMongoClient("mongodb://test")
    mongo_client.database = FakeDatabase(ping_error=ConnectionError("database unavailable"))

    monkeypatch.setattr(stream_service.nebula, "get_required_config", lambda: (123, "hash", ["ok"]))
    monkeypatch.setattr(stream_service, "Client", lambda name, **options: bots.append(FakeBot(name, **options)) or bots[-1])
    monkeypatch.setattr(stream_service, "AsyncIOMotorClient", lambda uri, **options: mongo_client)
    monkeypatch.setenv("MONGODB", "mongodb://test")
    monkeypatch.setenv("SESSIONS_DIR", str(tmp_path / "sessions"))

    with pytest.raises(ConnectionError, match="database unavailable"):
        await stream_service.run()

    assert bots[0].started and bots[0].stopped
    assert mongo_client.closed


def _async_return(value):
    async def return_value(*_args, **_kwargs):
        return value

    return return_value
