from __future__ import annotations

import asyncio
from stat import S_IFREG
from pathlib import Path
from pathlib import PurePosixPath
from types import SimpleNamespace

import pytest

from ftp.errors import PathIOError
from ftp.server import AbstractUserManager, Server, User

from test_ftp_server_protocol import make_connection, make_server


class CommandPathIO:
    def __init__(self):
        self.directories = set()
        self.files = {PurePosixPath("/raphael/readable.mkv")}
        self.renames = []
        self.unlinked = []
        self.fail_rmdir = False

    async def mkdir(self, path, exist_ok=False):
        self.directories.add(PurePosixPath(path))

    async def is_dir(self, path):
        return PurePosixPath(path) in self.directories

    async def exists(self, path):
        path = PurePosixPath(path)
        return path in self.files or path in self.directories

    async def is_file(self, path):
        return PurePosixPath(path) in self.files

    async def rmdir(self, path):
        if self.fail_rmdir:
            raise PathIOError("nonempty")
        self.directories.discard(PurePosixPath(path))

    async def stat(self, path):
        if not await self.exists(path):
            raise PathIOError("missing")
        return SimpleNamespace(st_size=321)

    async def unlink(self, path):
        self.unlinked.append(PurePosixPath(path))
        self.files.discard(PurePosixPath(path))

    async def rename(self, source, target):
        self.renames.append((PurePosixPath(source), PurePosixPath(target)))


def connection_with_path_io(path_io=None, **kwargs):
    kwargs.setdefault("tls_upgraded", False)
    connection = make_connection(path_io=path_io or CommandPathIO(), **kwargs)
    if "user" not in kwargs:
        connection.user.base_path = PurePosixPath("/")
    return connection


@pytest.mark.asyncio
async def test_password_command_authenticates_and_creates_user_home(tmp_path):
    server = make_server()
    user = User("raphael", "password")
    user.base_path = tmp_path
    path_io = CommandPathIO()
    connection = connection_with_path_io(user=user, path_io=path_io, logged=False)
    del connection.logged

    class Manager:
        async def authenticate(self, _user, password):
            return password == "password"

    server.user_manager = Manager()
    await server.pass_(connection, "password")
    assert connection.logged
    assert connection.current_directory == PurePosixPath("/raphael")
    assert connection.responses[-1] == ("230", "ok")
    assert PurePosixPath("/raphael") in path_io.directories


@pytest.mark.asyncio
async def test_password_command_rejects_wrong_password_without_login():
    server = make_server()
    connection = connection_with_path_io(logged=False)
    del connection.logged

    class Manager:
        async def authenticate(self, _user, _password):
            return False

    server.user_manager = Manager()
    await server.pass_(connection, "wrong")
    assert not connection.future.logged.done()
    assert connection.responses[-1] == ("530", "wrong pass")


@pytest.mark.asyncio
async def test_user_command_requires_tls_for_explicit_ftps():
    server = make_server(mode="ftps-explicit")
    connection = connection_with_path_io(tls_upgraded=False)
    await server.user(connection, "new-user")
    assert connection.responses == [("534", "AUTH TLS required before login")]


@pytest.mark.asyncio
async def test_user_command_sets_authenticated_home_after_manager_lookup(tmp_path):
    server = make_server()
    new_user = User("new-user", "secret")
    new_user.base_path = tmp_path
    connection = connection_with_path_io()

    class Manager:
        async def notify_logout(self, _user):
            pass

        async def get_user(self, login):
            assert login == "new-user"
            return AbstractUserManager.GetUserResponse.PASSWORD_REQUIRED, new_user, "password required"

    server.user_manager = Manager()
    await server.user(connection, "new-user")
    assert connection.user is new_user
    assert connection.current_directory == PurePosixPath("/new-user")
    assert connection.responses[-1] == ("331", "password required")


@pytest.mark.asyncio
async def test_cwd_creates_missing_directory_inside_user_home(tmp_path):
    user = User("raphael", "password")
    user.base_path = tmp_path
    path_io = CommandPathIO()
    connection = connection_with_path_io(user=user, path_io=path_io)
    await make_server().cwd(connection, "Doramas")
    assert connection.current_directory == PurePosixPath("/raphael/Doramas")
    expected_real_path = (tmp_path / "raphael" / "Doramas").resolve()
    created_real_paths = {Path(str(path)).resolve() for path in path_io.directories}
    assert expected_real_path in created_real_paths
    assert connection.responses[-1] == ("250", "ok")


@pytest.mark.asyncio
async def test_mkd_and_rmd_return_protocol_status_for_success_and_failure():
    path_io = CommandPathIO()
    connection = connection_with_path_io(path_io=path_io)
    server = make_server()
    await server.mkd(connection, "created")
    assert connection.responses[-1] == ("257", "ok")
    path_io.fail_rmdir = True
    await server.rmd(connection, "created")
    assert connection.responses[-1] == ("550", "directory is not empty or cannot be removed")
    path_io.fail_rmdir = False
    await server.rmd(connection, "created")
    assert connection.responses[-1] == ("250", "ok")


@pytest.mark.asyncio
async def test_size_mdtm_and_mlst_handle_missing_metadata_without_crashing():
    connection = connection_with_path_io()
    server = make_server()
    await server.size(connection, "not-found.mkv")
    assert connection.responses[-1] == ("213", "0")
    await server.mdtm(connection, "not-found.mkv")
    assert connection.responses[-1][0] == "213"
    await server.mlst(connection, "not-found.mkv")
    assert connection.responses[-1][0] == "250"
    assert "Type=file;Size=0" in connection.responses[-1][1][1]


@pytest.mark.asyncio
async def test_dele_is_idempotent_for_path_io_errors_and_returns_ftp_success():
    connection = connection_with_path_io()
    await make_server().dele(connection, "readable.mkv")
    assert connection.responses[-1] == ("250", "deleted")
    assert connection.path_io.unlinked == [PurePosixPath("/raphael/readable.mkv")]


@pytest.mark.asyncio
async def test_rename_from_and_rename_to_preserve_virtual_paths():
    connection = connection_with_path_io()
    server = make_server()
    await server.rnfr(connection, "readable.mkv")
    assert connection.responses[-1] == ("350", "pending")
    await server.rnto(connection, "renamed.mkv")
    assert connection.path_io.renames == [
        (PurePosixPath("/raphael/readable.mkv"), PurePosixPath("/raphael/renamed.mkv"))
    ]
    assert connection.responses[-1] == ("250", "renamed")


@pytest.mark.asyncio
async def test_feat_advertises_tls_only_when_explicit_mode_is_configured():
    ftp = connection_with_path_io()
    ftps = connection_with_path_io()
    await make_server().feat(ftp, "")
    await make_server(mode="ftps-explicit").feat(ftps, "")
    assert "AUTH TLS" not in ftp.responses[-1][1]
    assert {"AUTH TLS", "PBSZ", "PROT"}.issubset(set(ftps.responses[-1][1]))


@pytest.mark.asyncio
async def test_auth_tls_rejects_unsupported_mode_and_command():
    connection = connection_with_path_io()
    await make_server().auth(connection, "TLS")
    assert connection.responses[-1] == ("431", "TLS not configured")

    server = make_server(mode="ftps-explicit")
    server.set_ssl_context(object())
    await server.auth(connection, "SSL")
    assert connection.responses[-1] == ("504", "only AUTH TLS is supported")


@pytest.mark.asyncio
async def test_pbsz_requires_tls_and_accepts_protected_session():
    server = make_server(mode="ftps-explicit")
    connection = connection_with_path_io(tls_upgraded=False)

    await server.pbsz(connection, "0")
    assert connection.responses[-1] == ("503", "PBSZ not allowed without TLS")

    connection.tls_upgraded = True
    await server.pbsz(connection, "0")
    assert connection.responses[-1] == ("200", "ok")


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("tls_upgraded", "argument", "expected", "protected"),
    [
        (False, "P", ("503", "PROT not allowed without TLS"), False),
        (True, "P", ("200", "ok"), True),
        (True, "C", ("200", "ok"), False),
        (True, "S", ("504", "only P/C accepted"), False),
    ],
)
async def test_prot_enforces_tls_and_only_accepts_private_or_clear_mode(
    tls_upgraded, argument, expected, protected
):
    server = make_server(mode="ftps-explicit")
    connection = connection_with_path_io(tls_upgraded=tls_upgraded, protected=False)

    await server.prot(connection, argument)

    assert connection.responses[-1] == expected
    assert connection.protected is protected


@pytest.mark.asyncio
async def test_restart_offset_resets_invalid_values_and_quit_closes_session():
    server = make_server()
    connection = connection_with_path_io(restart_offset=9)

    await server.rest(connection, "1234")
    assert connection.restart_offset == 1234
    assert connection.responses[-1] == ("350", "restart")

    await server.rest(connection, "-1")
    assert connection.restart_offset == 0
    assert connection.responses[-1] == ("350", "restart")

    keep_serving = await server.quit(connection, "")
    assert keep_serving is False
    assert connection.responses[-1] == ("221", "bye")


@pytest.mark.asyncio
async def test_abort_cancels_all_active_workers_and_returns_completion():
    server = make_server()
    worker_a = asyncio.get_running_loop().create_future()
    worker_b = asyncio.get_running_loop().create_future()
    connection = connection_with_path_io(extra_workers={worker_a, worker_b})

    await server.abor(connection, "")

    assert worker_a.cancelled() and worker_b.cancelled()
    assert connection.responses[-1] == ("226", "abor")


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("command", "expected_prefix"),
    [("list", "-rw-r--r--"), ("mlsd", "type=file;size=321;modify=")],
)
async def test_directory_listing_workers_send_entries_and_complete_transfer(command, expected_prefix):
    class ListingPathIO(CommandPathIO):
        async def list(self, _path):
            yield PurePosixPath("/raphael/readable.mkv")

        async def stat(self, _path):
            return SimpleNamespace(st_mode=S_IFREG | 0o644, st_nlink=1, st_size=321, st_mtime=0)

    class DataStream:
        def __init__(self):
            self.written = bytearray()

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        async def write(self, data):
            self.written.extend(data)

    stream = DataStream()
    connection = connection_with_path_io(
        path_io=ListingPathIO(), passive_server=object(), data_connection=stream
    )
    server = make_server()

    await getattr(server, command)(connection, "/")
    assert connection.responses[-1] == ("150", "listing")
    await asyncio.gather(*connection.extra_workers)

    listing = stream.written.decode("utf-8")
    assert listing.startswith(expected_prefix)
    assert "321" in listing
    assert "readable.mkv\r\n" in listing
    assert connection.responses[-1] == ("226", "done")


@pytest.mark.asyncio
@pytest.mark.parametrize("command", ["list", "mlsd"])
async def test_directory_listing_worker_maps_filesystem_failure_to_451(command):
    class FailingListingPathIO(CommandPathIO):
        async def list(self, _path):
            raise OSError("listing failed")
            yield  # Keep this an async generator.

    class DataStream:
        async def __aenter__(self):
            return self

        async def __aexit__(self, *_args):
            return None

        async def write(self, _data):
            return None

    connection = connection_with_path_io(
        path_io=FailingListingPathIO(), passive_server=object(), data_connection=DataStream()
    )
    server = make_server()

    await getattr(server, command)(connection, "/")
    await asyncio.gather(*connection.extra_workers)

    assert connection.responses[-1] == ("451", "transfer error")
