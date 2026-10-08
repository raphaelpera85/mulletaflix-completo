from __future__ import annotations

import asyncio
from pathlib import PurePosixPath
from socket import AF_INET
from types import SimpleNamespace

import pytest

from ftp.server import AbstractUserManager, Connection, MongoDBUserManager, Server, User


class Stream:
    def __init__(self, data=b""):
        self.data = data
        self.output = bytearray()

    async def readline(self):
        data, self.data = self.data, b""
        return data

    async def write(self, data):
        self.output.extend(data)


def make_server(*, mode="ftp", user_manager=None):
    return Server(user_manager or SimpleNamespace(), object(), security_mode=mode)


def make_connection(**kwargs):
    responses = []
    kwargs.setdefault("response", lambda *args: responses.append(args))
    kwargs.setdefault("current_directory", PurePosixPath("/raphael"))
    kwargs.setdefault("logged", True)
    kwargs.setdefault("user", User("raphael", "password"))
    kwargs.setdefault("extra_workers", set())
    connection = Connection(**kwargs)
    connection.responses = responses
    return connection


class DataStream:
    def __init__(self, payload=b""):
        self.payload = payload
        self.written = bytearray()

    async def __aenter__(self):
        return self

    async def __aexit__(self, *_args):
        return None

    async def write(self, data):
        self.written.extend(data)


class MemoryFileOut:
    def __init__(self, path, mode):
        self.path = path
        self.mode = mode
        self.payload = bytearray()

    async def __aenter__(self):
        return self

    async def __aexit__(self, *_args):
        return None

    async def seek(self, offset):
        self.offset = offset

    async def write_stream(self, stream):
        self.payload.extend(stream.payload)


class MemoryFileIn:
    def __init__(self, payload):
        self.payload = payload

    async def __aenter__(self):
        return self

    async def __aexit__(self, *_args):
        return None

    async def seek(self, offset):
        self.offset = offset

    async def iter_by_block(self, _block_size):
        yield self.payload


class MemoryPathIO:
    def __init__(self, payload=b""):
        self.payload = payload
        self.opened = []
        self.directories = []

    async def mkdir(self, path, exist_ok=False):
        self.directories.append((path, exist_ok))

    async def is_dir(self, _path):
        return True

    async def exists(self, _path):
        return True

    async def is_file(self, _path):
        return True

    async def open(self, path, mode="rb"):
        self.opened.append((path, mode))
        if "r" in mode:
            return MemoryFileIn(self.payload)
        return MemoryFileOut(path, mode)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("line", "expected"),
    [
        ("CWD Série\r\n".encode(), ("cwd", "Série")),
        (b"NOOP\r\n", ("noop", "")),
        (b"CWD S\xe9rie\r\n", ("cwd", "Série")),
    ],
)
async def test_parse_command_normalizes_utf8_and_legacy_latin1(line, expected):
    assert await make_server().parse_command(Stream(line)) == expected


@pytest.mark.asyncio
async def test_parse_command_reports_closed_control_connection():
    with pytest.raises(ConnectionResetError):
        await make_server().parse_command(Stream())


@pytest.mark.asyncio
async def test_write_response_formats_multiline_and_utf8():
    stream = Stream()
    await make_server().write_response(stream, "211", ["features", "UTF8", "end"], list=True)
    assert stream.output.decode("utf-8") == "211-features\r\n UTF8\r\n211 end\r\n"


@pytest.mark.asyncio
async def test_get_paths_clamps_parent_traversal_to_authenticated_home(tmp_path):
    user = User("raphael", "password")
    user.base_path = tmp_path
    connection = make_connection(user=user, current_directory=PurePosixPath("/raphael"))

    real, virtual = Server.get_paths(connection, "../../etc/passwd")

    assert virtual == PurePosixPath("/raphael/etc/passwd")
    assert real == tmp_path / "raphael" / "etc" / "passwd"


def test_user_from_dict_ignores_legacy_and_malformed_acl_entries():
    user = User.from_dict({
        "login": "raphael",
        "password": "legacy",
        "permissions": [
            "elradfmwM",
            {"path": "relative", "writable": True},
            {"path": "/raphael/../private", "writable": True},
            {"path": "/shared", "readable": True},
        ],
    })

    assert user.check_password("legacy")
    assert {perm.path for perm in user.permissions} == {
        PurePosixPath("/raphael"), PurePosixPath("/"), PurePosixPath("/shared")
    }


class UsersCollection:
    def __init__(self, document):
        self.document = document
        self.updates = []

    async def find_one(self, query):
        return self.document if query.get("login") == self.document.get("login") else None

    async def update_one(self, query, update):
        self.updates.append((query, update))


@pytest.mark.asyncio
async def test_mongo_user_manager_migrates_legacy_plaintext_after_successful_auth(monkeypatch):
    from ftp import auth

    monkeypatch.setattr(auth, "_BCRYPT_ROUNDS", 4)
    users = UsersCollection({"login": "raphael", "password": "legacy"})
    manager = MongoDBUserManager(SimpleNamespace(users=users))
    state, user, _info = await manager.get_user("raphael")

    assert state == manager.GetUserResponse.PASSWORD_REQUIRED
    assert await manager.authenticate(user, "legacy")
    assert len(users.updates) == 1
    assert users.updates[0][1]["$unset"] == {"password": ""}
    assert auth.verify_password("legacy", users.updates[0][1]["$set"]["password_hash"])
    await manager.notify_logout(user)
    assert manager.users == []


@pytest.mark.asyncio
async def test_mongo_user_manager_enforces_connection_ceiling():
    users = UsersCollection({"login": "raphael", "password": "legacy"})
    manager = MongoDBUserManager(SimpleNamespace(users=users))

    for _ in range(100):
        state, _user, _info = await manager.get_user("raphael")
        assert state == manager.GetUserResponse.PASSWORD_REQUIRED
    state, _user, info = await manager.get_user("raphael")
    assert state == manager.GetUserResponse.ERROR
    assert info == "too much connections"


@pytest.mark.asyncio
async def test_ftps_server_refuses_to_start_without_tls_context():
    with pytest.raises(RuntimeError, match="FTPS mode requires"):
        await make_server(mode="ftps-explicit").start("127.0.0.1", 0)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("mode", "control_tls"),
    [("ftps-explicit", None), ("ftps-implicit", "configured-context")],
)
async def test_server_start_uses_tls_only_for_implicit_control_channel(
    monkeypatch, mode, control_tls
):
    from ftp import server as ftp_server

    observed = {}
    listener = SimpleNamespace(sockets=[])

    async def fake_start_server(callback, host, port, **kwargs):
        observed.update(callback=callback, host=host, port=port, kwargs=kwargs)
        return listener

    monkeypatch.setattr(ftp_server, "start_server", fake_start_server)
    server = make_server(mode=mode)
    server.set_ssl_context("configured-context")

    await server.start("127.0.0.1", 9021, limit=4096)

    assert server.server is listener
    assert observed["callback"].__self__ is server
    assert (observed["host"], observed["port"]) == ("127.0.0.1", 9021)
    assert observed["kwargs"]["limit"] == 4096
    assert observed["kwargs"]["ssl"] == control_tls


@pytest.mark.asyncio
async def test_passive_listener_retries_ports_and_uses_tls_for_protected_data(monkeypatch):
    from ftp import server as ftp_server

    calls = []
    listener = object()
    server = make_server(mode="ftps-explicit")
    server.set_ssl_context("tls-context")
    server.passive_ports = (41001, 41002)
    server._start_server_extra_arguments = {"limit": 2048}

    async def fake_start_server(_handler, host, port, **kwargs):
        calls.append((host, port, kwargs))
        if port == 41001:
            raise OSError("port in use")
        return listener

    monkeypatch.setattr(ftp_server, "start_server", fake_start_server)

    result = await server._start_passive_listener(object(), "127.0.0.1", protected=True)

    assert result is listener
    assert [call[1] for call in calls] == [41001, 41002]
    assert all(call[2]["ssl"] == "tls-context" for call in calls)
    assert all(call[2]["limit"] == 2048 for call in calls)


@pytest.mark.asyncio
async def test_passive_listener_returns_none_after_all_ports_are_unavailable(monkeypatch):
    from ftp import server as ftp_server

    calls = []
    server = make_server()
    server.passive_ports = (42001, 42002)
    server._start_server_extra_arguments = {}

    async def occupied(_handler, _host, port, **_kwargs):
        calls.append(port)
        raise OSError("port in use")

    monkeypatch.setattr(ftp_server, "start_server", occupied)

    assert await server._start_passive_listener(object(), "127.0.0.1") is None
    assert calls == [42001, 42002]


@pytest.mark.asyncio
async def test_disconnect_user_cancels_only_matching_logged_in_connections():
    server = make_server()
    stopped = asyncio.Event()
    both_started = asyncio.Event()
    started_count = 0

    async def hold_connection():
        nonlocal started_count
        started_count += 1
        if started_count == 2:
            both_started.set()
        try:
            await asyncio.Event().wait()
        finally:
            stopped.set()

    matching = make_connection(user=User("raphael", "password"))
    other = make_connection(user=User("other", "password"))
    matching._dispatcher = asyncio.create_task(hold_connection())
    other._dispatcher = asyncio.create_task(hold_connection())
    server.connections = {"matching": matching, "other": other}
    await both_started.wait()

    await server.disconnect_user("raphael")

    assert matching._dispatcher.cancelled()
    assert stopped.is_set()
    assert not other._dispatcher.done()
    other._dispatcher.cancel()
    await asyncio.gather(other._dispatcher, return_exceptions=True)


@pytest.mark.asyncio
async def test_dispatcher_flushes_greeting_and_quit_then_releases_connection():
    class Reader:
        def __init__(self):
            self.lines = iter((b"QUIT\r\n",))
            self.blocked = asyncio.Event()

        async def readline(self):
            try:
                return next(self.lines)
            except StopIteration:
                await self.blocked.wait()
                return b""

    class Transport:
        def get_extra_info(self, name, default=None):
            return {
                "peername": ("127.0.0.1", 50000),
                "sockname": ("127.0.0.1", 9021),
            }.get(name, default)

    class Writer:
        def __init__(self):
            self.transport = Transport()
            self.output = bytearray()
            self.closed = False

        def write(self, data):
            self.output.extend(data)

        async def drain(self):
            return None

        def close(self):
            self.closed = True

    server = make_server()
    server.server_port = 9021
    server.connections = {}
    server.path_io_factory = lambda **_kwargs: SimpleNamespace(state=None)
    writer = Writer()

    await server.dispatcher(Reader(), writer)

    replies = writer.output.decode("utf-8").splitlines()
    assert replies == ["220 Nebula FTP", "221 bye"]
    assert writer.closed
    assert server.connections == {}


@pytest.mark.asyncio
async def test_loopback_control_channel_handles_noop_and_quit_over_real_tcp():
    """Exercise the FTP control protocol through asyncio's real TCP listeners."""
    class PathIO:
        def __init__(self, connection=None, *, state=None):
            self.connection = connection
            self.state = state
            self.created_directories = []

        async def mkdir(self, path, exist_ok=False):
            self.created_directories.append((path, exist_ok))

    class UserManager:
        async def get_user(self, login):
            return (
                AbstractUserManager.GetUserResponse.PASSWORD_REQUIRED,
                User(login, "secret"),
                "password required",
            )

        async def authenticate(self, user, password):
            return user.check_password(password)

        async def notify_logout(self, _user):
            return None

    server = Server(UserManager(), PathIO)
    await server.start("127.0.0.1", 0)

    reader = writer = None
    try:
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection("127.0.0.1", server.server_port), timeout=2
        )

        greeting = await asyncio.wait_for(reader.readline(), timeout=2)
        assert greeting == b"220 Nebula FTP\r\n"

        writer.write(b"USER raphael\r\n")
        await writer.drain()
        assert await asyncio.wait_for(reader.readline(), timeout=2) == b"331 password required\r\n"

        writer.write(b"PASS secret\r\n")
        await writer.drain()
        assert await asyncio.wait_for(reader.readline(), timeout=2) == b"230 ok\r\n"

        writer.write(b"PWD\r\n")
        await writer.drain()
        assert await asyncio.wait_for(reader.readline(), timeout=2) == b'257 "/raphael"\r\n'

        writer.write(b"NOOP\r\n")
        await writer.drain()
        assert await asyncio.wait_for(reader.readline(), timeout=2) == b"200 ok\r\n"

        writer.write(b"QUIT\r\n")
        await writer.drain()
        assert await asyncio.wait_for(reader.readline(), timeout=2) == b"221 bye\r\n"
        assert await asyncio.wait_for(reader.read(), timeout=2) == b""
    finally:
        if writer is not None:
            writer.close()
            await writer.wait_closed()
        await server.close()

    assert server.connections == {}
    assert server.available_connections.value == 256


@pytest.mark.asyncio
async def test_ftps_explicit_login_and_pbsz_prot_require_tls():
    server = make_server(mode="ftps-explicit")
    connection = make_connection(tls_upgraded=False, protected=False)

    await server.user(connection, "raphael")
    await server.pbsz(connection, "0")
    await server.prot(connection, "P")

    assert [response[0] for response in connection.responses] == ["534", "503", "503"]
    assert not connection.protected


@pytest.mark.asyncio
async def test_ftps_prot_accepts_private_or_clear_data_channel_only_after_tls():
    server = make_server(mode="ftps-explicit")
    connection = make_connection(tls_upgraded=True)

    await server.pbsz(connection, "0")
    await server.prot(connection, "P")
    await server.prot(connection, "invalid")
    await server.prot(connection, "C")

    assert [response[0] for response in connection.responses] == ["200", "200", "504", "200"]
    assert connection.protected is False


@pytest.mark.asyncio
async def test_rest_invalid_offset_resets_to_zero():
    connection = make_connection(restart_offset=12)

    await make_server().rest(connection, "not-a-number")

    assert connection.restart_offset == 0
    assert connection.responses[-1] == ("350", "restart")


@pytest.mark.asyncio
@pytest.mark.parametrize(("epsv", "code", "message"), [(True, "229", "entering epsv (|||4567|)"), (False, "227", "entering pasv (127,0,0,1,17,215)")])
async def test_passive_mode_formats_epsv_and_pasv_replies(monkeypatch, epsv, code, message):
    server = make_server()
    passive = SimpleNamespace(sockets=[SimpleNamespace(family=AF_INET, getsockname=lambda: ("127.0.0.1", 4567))])

    async def listener(*_args, **_kwargs):
        return passive

    monkeypatch.setattr(server, "_start_passive_listener", listener)
    connection = make_connection(server_host="127.0.0.1", protected=False)

    assert await server._pasv_common(connection, epsv)
    assert connection.responses[-1] == (code, message)


@pytest.mark.asyncio
async def test_passive_mode_reports_unavailable_port_range(monkeypatch):
    server = make_server()
    async def no_listener(*_args, **_kwargs):
        return None
    monkeypatch.setattr(server, "_start_passive_listener", no_listener)
    connection = make_connection(server_host="127.0.0.1", protected=False)

    assert not await server._pasv_common(connection, True)
    assert connection.responses[-1] == ("421", "no ports")


@pytest.mark.asyncio
async def test_stor_handler_transfers_data_and_reports_completion(tmp_path):
    server = make_server()
    path_io = MemoryPathIO()
    stream = DataStream(b"incoming-media")
    user = User("raphael", "password")
    user.base_path = tmp_path
    connection = make_connection(
        user=user,
        path_io=path_io,
        passive_server=object(),
        data_connection=stream,
        restart_offset=0,
    )

    await server.stor(connection, "Series/episode.mkv")
    await next(iter(connection.extra_workers))

    assert connection.responses[0] == ("150", "upload starting")
    assert connection.responses[-1] == ("226", "transfer complete")
    assert path_io.opened[0][1] == "wb"
    assert path_io.directories
    worker = next(iter(connection.extra_workers))
    assert worker.done()


@pytest.mark.asyncio
async def test_retr_handler_streams_file_bytes_and_honors_restart_offset(tmp_path):
    server = make_server()
    path_io = MemoryPathIO(b"outgoing-media")
    stream = DataStream()
    user = User("raphael", "password")
    user.base_path = tmp_path
    connection = make_connection(
        user=user,
        path_io=path_io,
        passive_server=object(),
        data_connection=stream,
        restart_offset=4,
    )

    await server.retr(connection, "Series/episode.mkv")
    await next(iter(connection.extra_workers))

    assert connection.responses == [("150", "download starting"), ("226", "transfer complete")]
    assert path_io.opened[0][1] == "rb"
    assert stream.written == b"outgoing-media"
    assert next(iter(connection.extra_workers)).done()


def test_tls_context_requires_both_certificate_and_key():
    assert make_server()._build_ssl_context("cert.pem", "") is None
