from __future__ import annotations

import asyncio
import importlib
import sys

import pytest
from bson import ObjectId

# Some legacy tests install a lightweight `ftp` package during collection.
# Restore the real package before importing the HTTP server module.
ftp_package = sys.modules.get("ftp")
if ftp_package is not None and not hasattr(ftp_package, "MongoDBPathIO"):
    for module_name in [name for name in sys.modules if name == "ftp" or name.startswith("ftp.")]:
        sys.modules.pop(module_name, None)
    importlib.import_module("ftp")

main = importlib.import_module("main")


class Writer:
    def __init__(self, peer="203.0.113.10"):
        self.peer = peer
        self.data = bytearray()

    def write(self, data):
        self.data.extend(data)

    async def drain(self):
        return None

    def get_extra_info(self, name, default=None):
        return (self.peer, 1234) if name == "peername" else default

    def close(self):
        return None

    async def wait_closed(self):
        return None


class Files:
    def __init__(self, doc):
        self.doc = doc

    async def find_one(self, _query, _projection=None):
        return self.doc


class Mongo:
    def __init__(self, doc):
        self.files = Files(doc)


class HierarchyFiles:
    def __init__(self, docs):
        self.docs = docs

    async def find_one(self, query, _projection=None):
        for doc in self.docs:
            if all(
                (key == "parts.0" and bool(doc.get("parts")) == value.get("$exists"))
                or (key != "parts.0" and doc.get(key) == value)
                for key, value in query.items()
            ):
                return doc
        return None


def body_after_headers(data):
    return bytes(data).partition(b"\r\n\r\n")[2]


@pytest.mark.asyncio
@pytest.mark.parametrize("method,expected_body", [("GET", True), ("HEAD", False)])
async def test_non_loopback_stream_requires_bearer(monkeypatch, method, expected_body):
    monkeypatch.setattr(main, "STREAM_TOKEN", "x" * 32)
    reader = asyncio.StreamReader()
    reader.feed_data(f"{method} / HTTP/1.1\r\nHost: test\r\n\r\n".encode())
    reader.feed_eof()
    writer = Writer()

    await main.handle_http_client(reader, writer, Mongo(None), [])

    assert bytes(writer.data).startswith(b"HTTP/1.1 401 Unauthorized")
    assert bool(body_after_headers(writer.data)) is expected_body


@pytest.mark.asyncio
async def test_non_loopback_stream_accepts_legacy_strm_query_token(monkeypatch):
    token = "x" * 32
    monkeypatch.setattr(main, "STREAM_TOKEN", token)
    reader = asyncio.StreamReader()
    reader.feed_data(f"HEAD /play?id=invalid&token={token} HTTP/1.1\r\nHost: test\r\n\r\n".encode())
    reader.feed_eof()
    writer = Writer()

    await main.handle_http_client(reader, writer, Mongo(None), [])

    assert bytes(writer.data).startswith(b"HTTP/1.1 400 Bad Request")


@pytest.mark.asyncio
async def test_head_error_does_not_write_body():
    writer = Writer("127.0.0.1")

    await main.http_player(writer, Mongo(None), "not-an-object-id", head_only=True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 400 Bad Request")
    assert body_after_headers(writer.data) == b""


@pytest.mark.asyncio
async def test_unsatisfiable_range_is_416_and_filename_cannot_inject_header():
    file_id = ObjectId()
    mongo = Mongo(
        {
            "_id": file_id,
            "type": "file",
            "status": "completed",
            "parts": [{"part_id": 1}],
            "size": 10,
            "name": 'movie"\r\nX-Injected: yes.mp4',
        }
    )
    writer = Writer("127.0.0.1")

    await main.stream_completed_file(writer, mongo, [], str(file_id), "bytes=20-30", True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 416 Range Not Satisfiable")
    assert b"Content-Range: bytes */10" in writer.data
    assert b"\r\nX-Injected:" not in writer.data
    assert body_after_headers(writer.data) == b""

    writer = Writer("127.0.0.1")
    await main.stream_completed_file(writer, mongo, [], str(file_id), None, True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert b"\r\nX-Injected:" not in writer.data
    assert b'Content-Disposition: inline; filename="movieX-Injected: yes.mp4"' in writer.data
    assert body_after_headers(writer.data) == b""


@pytest.mark.asyncio
async def test_stream_accepts_nested_virtual_path_with_bson_parent(monkeypatch):
    folder_id, media_id = ObjectId(), ObjectId()
    docs = [
        {"_id": folder_id, "type": "dir", "name": "Series", "parent": None},
        {
            "_id": media_id,
            "type": "file",
            "status": "completed",
            "parts": [{"part_id": 1}],
            "size": 100,
            "name": "Episode.mkv",
            "parent": folder_id,
        },
    ]
    mongo = type("Mongo", (), {"files": HierarchyFiles(docs)})()
    monkeypatch.setattr(main.MongoDBPathIO, "db", mongo)
    writer = Writer("127.0.0.1")

    await main.stream_completed_file(writer, mongo, [], "Series/Episode.mkv", None, True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert b"Content-Length: 100" in writer.data
