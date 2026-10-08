from __future__ import annotations

import asyncio
from pathlib import Path, PurePosixPath
from types import SimpleNamespace

import pytest

from ftp import pathio, server


class _Stream:
    def __init__(self, *chunks: bytes):
        self.chunks = chunks

    async def iter_by_block(self, _block_size):
        for chunk in self.chunks:
            yield chunk


class _Files:
    async def replace_one(self, *_args, **_kwargs):
        return None

    async def delete_one(self, *_args, **_kwargs):
        return None


class _Database:
    files = _Files()


class _RenameIO:
    def __init__(self, existing=()):
        self.existing = set(existing)
        self.renames = []

    async def exists(self, path):
        return path in self.existing

    async def rename(self, source, destination):
        self.renames.append((source, destination))


def _connection(*, logged: bool | None, io, user=None, rename_from=None):
    replies = []
    values = {
        "current_directory": PurePosixPath("/alice"),
        "path_io": io,
        "response": lambda *args: replies.append(args),
    }
    if logged is not None:
        values["logged"] = logged
    if user is not None:
        values["user"] = user
    if rename_from is not None:
        values["rename_from"] = rename_from
    return server.Connection(**values), replies


@pytest.mark.parametrize("command", ["rnfr", "rnto"])
@pytest.mark.asyncio
async def test_rename_commands_require_login(command):
    ftp_server = server.Server(user_manager=None, path_io=object())
    conn, replies = _connection(logged=None, io=object())

    await getattr(ftp_server, command)(conn, "/alice/source.mkv")

    assert replies == [("503", "bad sequence (not logged in)")]


@pytest.mark.asyncio
async def test_rnfr_rejects_source_outside_writable_acl():
    ftp_server = server.Server(user_manager=None, path_io=object())
    io = _RenameIO(existing={Path("alice/locked/source.mkv")})
    user = server.User("alice", "pw", [server.Permission("/alice/locked", readable=True, writable=False)])
    conn, replies = _connection(logged=True, io=io, user=user)

    await ftp_server.rnfr(conn, "/locked/source.mkv")

    assert replies == [("550", "permission denied")]
    assert "rename_from" not in conn


@pytest.mark.asyncio
async def test_rnto_requires_a_successful_rnfr_before_renaming():
    ftp_server = server.Server(user_manager=None, path_io=object())
    io = _RenameIO()
    user = server.User("alice", "pw")
    conn, replies = _connection(logged=True, io=io, user=user)

    await ftp_server.rnto(conn, "/destination.mkv")

    assert replies == [("503", "bad sequence (no filename)")]
    assert io.renames == []


@pytest.mark.asyncio
async def test_authorized_rename_requires_existing_source_and_writable_destination():
    ftp_server = server.Server(user_manager=None, path_io=object())
    source = Path("alice/source.mkv")
    io = _RenameIO(existing={source})
    user = server.User("alice", "pw")
    conn, replies = _connection(logged=True, io=io, user=user)

    await ftp_server.rnfr(conn, "/source.mkv")
    await ftp_server.rnto(conn, "/renamed.mkv")

    assert replies == [("350", "pending"), ("250", "renamed")]
    assert io.renames == [(source, Path("alice/renamed.mkv"))]


def test_resumed_upload_preserves_prefix(tmp_path, monkeypatch):
    monkeypatch.setattr(pathio, "get_cache_dir", lambda: str(tmp_path))
    node = SimpleNamespace(name="episode.mkv", parent="/alice", parts=[])
    writer = pathio.MongoDBMemoryIO(node, "r+b", tg=[], db=_Database())

    async def transfer():
        await writer.write_stream(_Stream(b"prefix"))
        resumed_node = SimpleNamespace(
            name="episode.mkv", parent="/alice", parts=[], local_path=writer.local_path
        )
        resumed_writer = pathio.MongoDBMemoryIO(resumed_node, "r+b", tg=[], db=_Database())
        await resumed_writer.seek(len(b"prefix"))
        await resumed_writer.write_stream(_Stream(b"-suffix"))
        return resumed_writer.local_path

    resumed_path = asyncio.run(transfer())

    assert resumed_path == writer.local_path
    assert Path(resumed_path).read_bytes() == b"prefix-suffix"
