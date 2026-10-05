from __future__ import annotations

import importlib
import hashlib
import asyncio
import json
from pathlib import PurePosixPath

import pytest
from bson import ObjectId

pathio = importlib.import_module("ftp.pathio")


class AsyncCursor:
    def __init__(self, items):
        self.items = iter(items)

    def __aiter__(self):
        return self

    async def __anext__(self):
        try:
            return next(self.items)
        except StopIteration:
            raise StopAsyncIteration


class Files:
    def __init__(self, docs):
        self.docs = docs

    async def find_one(self, query, _projection=None):
        for doc in self.docs:
            if all(doc.get(key) == value for key, value in query.items()):
                return doc
        return None

    def find(self, query):
        parent_values = query["parent"]["$in"]
        return AsyncCursor(
            doc for doc in self.docs
            if doc.get("parent") in parent_values
            and not doc["name"].endswith(".partial")
        )

    async def replace_one(self, query, replacement, upsert=False):
        for index, doc in enumerate(self.docs):
            if all(doc.get(key) == value for key, value in query.items()):
                self.docs[index] = replacement
                return
        if upsert:
            self.docs.append(replacement)


class Database:
    def __init__(self, docs):
        self.files = Files(docs)


@pytest.mark.asyncio
async def test_stream_reads_prefetched_cache_chunk_before_telegram(tmp_path, monkeypatch):
    media_id = ObjectId()
    cache_root = tmp_path / "nebula-playback"
    cache_key = hashlib.sha256(f"mongo:{media_id}".encode()).hexdigest().upper()
    cache_dir = cache_root / cache_key
    cache_dir.mkdir(parents=True)
    cached = b"a" * 32
    (cache_dir / "000001-00000000.bin").write_bytes(cached)
    monkeypatch.setenv("NEBULA_PLAYBACK_CACHE_ROOT", str(cache_root))
    monkeypatch.setenv("NEBULA_CACHE_LEASE_TOKEN", "lease-token")

    async def handle_lease(reader, writer):
        request = json.loads(await reader.readline())
        assert request["action"] == "acquire"
        assert request["mediaId"] == str(media_id)
        writer.write(b'{"ok":true,"leaseId":"test-lease"}\n')
        await writer.drain()
        release = json.loads(await reader.readline())
        assert release["action"] == "release"
        writer.write(b'{"ok":true}\n')
        await writer.drain()
        writer.close()
        await writer.wait_closed()

    lease_server = await asyncio.start_server(handle_lease, "127.0.0.1", 0)
    monkeypatch.setenv("NEBULA_CACHE_LEASE_PORT", str(lease_server.sockets[0].getsockname()[1]))
    node = pathio.Node(
        "file", "Cached.mkv", size=len(cached), parts=[{"part_id": 1, "file_size": len(cached)}], _id=media_id
    )
    reader = pathio.MongoDBMemoryIO(node, "rb", [], Database([]))

    async with lease_server:
        chunks = [chunk async for chunk in reader.iter_by_block(11)]

    assert b"".join(chunks) == cached


@pytest.mark.asyncio
@pytest.mark.parametrize("parent_format", ["legacy", "object-id", "mixed"])
async def test_nested_items_from_either_nebula_are_resolved_and_listed(parent_format):
    root_id, season_id = ObjectId(), ObjectId()
    if parent_format == "legacy":
        root_parent, season_parent, episode_parent = "/", "/Series", "/Series/Season 1"
    elif parent_format == "object-id":
        root_parent, season_parent, episode_parent = None, root_id, season_id
    else:
        root_parent, season_parent, episode_parent = "/", root_id, "/Series/Season 1"
    docs = [
        {"_id": root_id, "type": "dir", "name": "Series", "parent": root_parent},
        {"_id": season_id, "type": "dir", "name": "Season 1", "parent": season_parent},
        {"_id": ObjectId(), "type": "file", "name": "Episode.mkv", "parent": episode_parent},
    ]
    io = pathio.MongoDBPathIO()
    io.db = Database(docs)
    async with io._cache_lock:
        io._memory_cache.clear()

    node = await io.get_node(PurePosixPath("/Series/Season 1/Episode.mkv"))
    listed = [entry async for entry in io.list(PurePosixPath("/Series/Season 1"))]

    assert node is not None
    assert node.name == "Episode.mkv"
    assert [entry.as_posix() for entry in listed] == ["/Series/Season 1/Episode.mkv"]


@pytest.mark.asyncio
async def test_upload_open_reuses_existing_bson_parent_file_instead_of_duplicating():
    root_id, season_id, file_id = ObjectId(), ObjectId(), ObjectId()
    docs = [
        {"_id": root_id, "type": "dir", "name": "Series", "parent": None},
        {"_id": season_id, "type": "dir", "name": "Season 1", "parent": root_id},
        {"_id": file_id, "type": "file", "name": "Episode.mkv", "parent": season_id, "size": 123},
    ]
    io = pathio.MongoDBPathIO()
    io.db = Database(docs)
    async with io._cache_lock:
        io._memory_cache.clear()

    await io.open(PurePosixPath("/Series/Season 1/Episode.mkv"), mode="wb")

    assert len(docs) == 3
    assert docs[-1]["_id"] == file_id
    assert docs[-1]["parent"] == "/Series/Season 1"
