from __future__ import annotations

import asyncio
import hashlib
import importlib
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
        self.find_queries = []

    async def find_one(self, query, _projection=None):
        for doc in self.docs:
            if all(doc.get(key) == value for key, value in query.items()):
                return doc
        return None

    def find(self, query):
        self.find_queries.append(query)
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
async def test_stream_rejects_truncated_telegram_part(monkeypatch):
    class Bot:
        name = "bot"
        _nebula_streams = 0

    bot = Bot()

    async def reserve(_candidates):
        bot._nebula_streams += 1
        return bot

    class FakeFile:
        reference_refreshed = False

        def __init__(self, *_args, **_kwargs):
            pass

        async def stream(self, offset=0):
            assert offset == 0
            yield b"short"

    monkeypatch.setattr(pathio, "File", FakeFile)
    monkeypatch.setattr(pathio, "resolve_part_bots", lambda *_args: [bot])
    monkeypatch.setattr(pathio, "reserve_free_stream_bot", reserve)
    node = pathio.Node(
        "file", "Truncated.mkv", size=10,
        parts=[{"part_id": 1, "file_size": 10, "tg_file": "file1"}],
    )
    reader = pathio.MongoDBMemoryIO(node, "rb", [bot], Database([]))

    with pytest.raises(OSError, match=r"was truncated \(5/10 bytes\)"):
        _ = [chunk async for chunk in reader.iter_by_block(4)]

    assert bot._nebula_streams == 0


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
        {
            "_id": ObjectId(), "type": "file", "name": "Episode.mkv", "parent": episode_parent,
            "parts": [{"tg_file_id": "published"}],
        },
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
async def test_listing_checks_many_directories_with_one_query_per_tree_level():
    shelves = [ObjectId() for _ in range(5)]
    seasons = [ObjectId() for _ in range(4)]
    docs = [
        {"_id": shelf_id, "type": "dir", "name": f"Shelf {index}", "parent": "/"}
        for index, shelf_id in enumerate(shelves)
    ]
    docs.append({"_id": ObjectId(), "type": "dir", "name": "Empty", "parent": "/"})
    for index, season_id in enumerate(seasons):
        docs.extend([
            {"_id": season_id, "type": "dir", "name": "Season 1", "parent": shelves[index]},
            {
                "_id": ObjectId(), "type": "file", "name": f"Episode {index}.mkv", "parent": season_id,
                "parts": [{"tg_file_id": f"published-{index}"}],
            },
        ])

    io = pathio.MongoDBPathIO()
    io.db = Database(docs)
    async with io._cache_lock:
        io._memory_cache.clear()

    listed = [entry.as_posix() async for entry in io.list(PurePosixPath("/"))]

    assert set(listed) == {f"/Shelf {index}" for index in range(4)}
    assert len(listed) == 4
    assert len(io._files.find_queries) == 3  # listed children, then each of two descendant levels


@pytest.mark.asyncio
async def test_listing_stops_when_a_malformed_directory_cycle_repeats_an_id():
    shelf_id, child_id = ObjectId(), ObjectId()
    io = pathio.MongoDBPathIO()
    io.db = Database([
        {"_id": child_id, "type": "dir", "name": "Loop", "parent": shelf_id},
        # Duplicate ID models corrupt/cyclic input; Mongo normally enforces unique _id.
        {"_id": shelf_id, "type": "dir", "name": "Shelf", "parent": child_id},
    ])

    visible = await io._published_listing_directories([
        ("/Shelf", {"_id": shelf_id, "type": "dir", "name": "Shelf", "parent": "/"}),
    ])

    assert visible == set()
    assert len(io._files.find_queries) == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("parent_alias", ["/Shelf", "Shelf", "/raphael/Shelf", "raphael/Shelf"])
async def test_listing_accepts_legacy_user_root_parent_aliases(parent_alias):
    shelf_id = ObjectId()
    io = pathio.MongoDBPathIO()
    io.db = Database([
        {"_id": shelf_id, "type": "dir", "name": "Shelf", "parent": "/"},
        {
            "_id": ObjectId(), "type": "file", "name": "Book.mkv", "parent": parent_alias,
            "parts": [{"tg_file_id": "published"}],
        },
    ])

    listed = [entry.as_posix() async for entry in io.list(PurePosixPath("/"))]

    assert listed == ["/Shelf"]


@pytest.mark.asyncio
async def test_listing_keeps_path_resolution_on_first_duplicate_directory():
    first_id, second_id = ObjectId(), ObjectId()
    io = pathio.MongoDBPathIO()
    io.db = Database([
        # BSON result appears first in the cursor, but the legacy parent is the
        # first representation selected by _parent_values_for_path("/").
        {"_id": second_id, "type": "dir", "name": "Shelf", "parent": None},
        {"_id": first_id, "type": "dir", "name": "Shelf", "parent": "/"},
        {
            "_id": ObjectId(), "type": "file", "name": "Book.mkv", "parent": second_id,
            "parts": [{"tg_file_id": "published"}],
        },
    ])

    listed = [entry.as_posix() async for entry in io.list(PurePosixPath("/"))]

    assert listed == []


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
