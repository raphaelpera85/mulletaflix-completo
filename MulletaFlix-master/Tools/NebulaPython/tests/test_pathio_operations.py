from __future__ import annotations

import asyncio
import importlib
import json
import os
from pathlib import Path, PurePosixPath

import pytest
from bson import ObjectId

pathio = importlib.import_module("ftp.pathio")


def matches(doc, query):
    for key, value in query.items():
        actual = doc.get(key)
        if isinstance(value, dict) and "$in" in value:
            if actual not in value["$in"]:
                return False
        elif isinstance(value, dict) and "$not" in value:
            pattern = value["$not"].get("$regex", "")
            if pattern.endswith("\\.partial$") and actual.endswith(".partial"):
                return False
        elif actual != value:
            return False
    return True


class FakeFiles:
    def __init__(self, docs=None):
        self.docs = list(docs or [])
        self.updates = []

    async def find_one(self, query, _projection=None):
        return next((doc for doc in self.docs if matches(doc, query)), None)

    def find(self, query):
        return AsyncCursor(doc for doc in self.docs if matches(doc, query))

    async def count_documents(self, query):
        return sum(matches(doc, query) for doc in self.docs)

    async def insert_one(self, doc):
        if await self.find_one({"name": doc["name"], "parent": doc["parent"]}):
            raise pathio.DuplicateKeyError("duplicate")
        doc.setdefault("_id", ObjectId())
        self.docs.append(doc)

    async def replace_one(self, query, replacement, upsert=False):
        for i, doc in enumerate(self.docs):
            if matches(doc, query):
                replacement.setdefault("_id", doc.get("_id"))
                self.docs[i] = replacement
                return
        if upsert:
            replacement.setdefault("_id", ObjectId())
            self.docs.append(replacement)

    async def delete_one(self, query):
        for i, doc in enumerate(self.docs):
            if matches(doc, query):
                self.docs.pop(i)
                return

    async def update_one(self, query, update):
        self.updates.append((query, update))
        doc = await self.find_one(query)
        if not doc:
            return
        doc.update(update.get("$set", {}))
        for key in update.get("$unset", {}):
            doc.pop(key, None)


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


class FakeDB:
    def __init__(self, docs=None):
        self.files = FakeFiles(docs)


class ByteStream:
    def __init__(self, chunks):
        self.chunks = chunks

    async def iter_by_block(self, _block_size):
        for chunk in self.chunks:
            yield chunk


@pytest.fixture
def io():
    instance = pathio.MongoDBPathIO()
    instance.db = FakeDB()
    instance.tg = []
    async def clear_cache():
        async with instance._cache_lock:
            instance._memory_cache.clear()
    asyncio.run(clear_cache())
    return instance


@pytest.mark.asyncio
async def test_mkdir_exist_ok_and_rmdir_only_empty_directories(io):
    folder = PurePosixPath("/Empty")
    await io.mkdir(folder)
    with pytest.raises(pathio.PathIOError):
        await io.mkdir(folder)
    await io.mkdir(folder, exist_ok=True)
    await io.rmdir(folder)
    assert not io.db.files.docs


def test_bounded_lru_cache_pop_removes_entries_and_honors_default():
    cache = pathio.BoundedLRUCache(maxsize=2)
    cache["first"] = 1
    cache["second"] = 2

    assert cache.pop("first") == 1
    assert list(cache) == ["second"]
    assert cache.pop("missing", None) is None
    with pytest.raises(KeyError, match="missing"):
        cache.pop("missing")


def test_bounded_lru_cache_popitem_evicts_oldest_without_lookup_error():
    cache = pathio.BoundedLRUCache(maxsize=1)
    cache["first"] = 1

    cache["second"] = 2

    assert list(cache.items()) == [("second", 2)]


@pytest.mark.asyncio
async def test_rmdir_refuses_nonempty_directory_without_deleting_children(io):
    folder_id = ObjectId()
    io.db.files.docs.extend([
        {"_id": folder_id, "type": "dir", "name": "Series", "parent": "/"},
        {"_id": ObjectId(), "type": "file", "name": "Episode.mkv", "parent": "/Series"},
    ])
    with pytest.raises(pathio.PathIOError):
        await io.rmdir(PurePosixPath("/Series"))
    assert len(io.db.files.docs) == 2


@pytest.mark.asyncio
async def test_list_excludes_partial_uploads_and_returns_virtual_paths(io):
    io.db.files.docs.extend([
        {"_id": ObjectId(), "type": "file", "name": "Ready.mkv", "parent": "/"},
        {"_id": ObjectId(), "type": "file", "name": "Pending.mkv.partial", "parent": "/"},
    ])
    listed = [p.as_posix() async for p in io.list(PurePosixPath("/"))]
    assert listed == ["/Ready.mkv"]


@pytest.mark.asyncio
async def test_unlink_removes_staged_file_and_document(io, tmp_path):
    local = tmp_path / "staged.mkv"
    local.write_bytes(b"data")
    doc_id = ObjectId()
    io.db.files.docs.append({
        "_id": doc_id, "type": "file", "name": "staged.mkv", "parent": "/", "local_path": str(local)
    })
    await io.unlink(PurePosixPath("/staged.mkv"))
    assert not local.exists()
    assert not io.db.files.docs


@pytest.mark.asyncio
async def test_unlink_missing_path_is_idempotent(io):
    await io.unlink(PurePosixPath("/missing.mkv"))


@pytest.mark.asyncio
async def test_stat_reports_file_and_missing_path_errors(io):
    io.db.files.docs.append({
        "_id": ObjectId(), "type": "file", "name": "one.mkv", "parent": "/", "size": 321,
        "ctime": 10, "mtime": 20,
    })
    info = await io.stat(PurePosixPath("/one.mkv"))
    assert (info.st_size, info.st_ctime, info.st_mtime) == (321, 10, 20)
    with pytest.raises(pathio.PathIOError):
        await io.stat(PurePosixPath("/missing.mkv"))


@pytest.mark.asyncio
async def test_open_read_missing_file_raises_path_io_error(io):
    with pytest.raises(pathio.PathIOError):
        await io.open(PurePosixPath("/missing.mkv"), "rb")


@pytest.mark.asyncio
async def test_resumed_upload_preserves_staged_prefix_and_upserts_metadata(io, tmp_path):
    staged = tmp_path / "resume.mkv.partial"
    staged.write_bytes(b"prefix-")
    node = pathio.Node("file", staged.name, parent="/", local_path=str(staged))
    writer = pathio.MongoDBMemoryIO(node, "r+b", [], io.db)
    await writer.seek(len(b"prefix-"))
    await writer.write_stream(ByteStream([b"suffix"]))
    saved = await io.db.files.find_one({"name": staged.name, "parent": "/"})
    assert staged.read_bytes() == b"prefix-suffix"
    assert saved["local_path"] == str(staged)
    assert saved["size"] == len(b"prefix-suffix")
    assert saved["status"] == "staging"


@pytest.mark.asyncio
async def test_empty_upload_is_removed_from_staging_and_database(io, tmp_path):
    staged = tmp_path / "empty.mkv"
    staged.write_bytes(b"old")
    io.db.files.docs.append({"_id": ObjectId(), "name": staged.name, "parent": "/"})
    node = pathio.Node("file", staged.name, parent="/", local_path=str(staged))
    writer = pathio.MongoDBMemoryIO(node, "r+b", [], io.db)
    await writer.write_stream(ByteStream([]))
    assert not staged.exists()
    assert await io.db.files.find_one({"name": staged.name, "parent": "/"}) is None


@pytest.mark.asyncio
async def test_non_uploadable_metadata_is_persisted_as_completed_without_staging_file(
    io, tmp_path, monkeypatch
):
    monkeypatch.setattr(pathio, "get_cache_dir", lambda: str(tmp_path))
    node = pathio.Node("file", "cover.nfo", parent="/Series/Show")
    writer = pathio.MongoDBMemoryIO(node, "wb", [], io.db)

    await writer.write_stream(ByteStream([b"metadata-content"]))

    assert not os.path.exists(writer.local_path)
    saved = await io.db.files.find_one({"name": "cover.nfo", "parent": "/Series/Show"})
    assert saved["size"] == len(b"metadata-content")
    assert saved["status"] == "completed"
    assert "local_path" not in saved
    cached = io._memory_cache["/Series/Show::cover.nfo"]
    assert cached["status"] == "completed"
    assert "local_path" not in cached


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "error_type",
    [pathio.ConnectionFailure, pathio.ServerSelectionTimeoutError, pathio.PyMongoError],
)
async def test_upload_keeps_staging_and_memory_cache_when_mongo_upsert_fails(
    io, tmp_path, monkeypatch, error_type
):
    monkeypatch.setattr(pathio, "get_cache_dir", lambda: str(tmp_path))
    node = pathio.Node("file", "episode.mkv.partial", parent="/Series/Show")
    writer = pathio.MongoDBMemoryIO(node, "wb", [], io.db)

    async def fail_upsert(*_args, **_kwargs):
        raise error_type("simulated database outage")

    io.db.files.replace_one = fail_upsert
    await writer.write_stream(ByteStream([b"video-bytes"]))

    assert Path(writer.local_path).read_bytes() == b"video-bytes"
    cached = io._memory_cache["/Series/Show::episode.mkv.partial"]
    assert cached["status"] == "staging"
    assert cached["local_path"] == writer.local_path
    assert cached["size"] == len(b"video-bytes")
    assert await io.get_node(PurePosixPath("/Series/Show/episode.mkv.partial")) is not None


@pytest.mark.asyncio
async def test_rename_completed_partial_enqueues_upload(io, tmp_path, monkeypatch):
    staged = tmp_path / "episode.partial"
    staged.write_bytes(b"episode")
    doc_id = ObjectId()
    io.db.files.docs.append({
        "_id": doc_id, "type": "file", "name": "episode.mkv.partial", "parent": "/",
        "size": 7, "local_path": str(staged),
    })
    enqueued = []

    class Queue:
        async def put(self, item):
            enqueued.append(item)

    monkeypatch.setattr(pathio, "UPLOAD_QUEUE", Queue())
    await io.rename(PurePosixPath("/episode.mkv.partial"), PurePosixPath("/episode.mkv"))
    assert enqueued == [{"path": str(staged), "filename": "episode.mkv", "parent": "/", "size": 7}]
    saved = await io.db.files.find_one({"_id": doc_id})
    assert saved["name"] == "episode.mkv"
    assert saved["local_path"] == str(staged)


@pytest.mark.asyncio
async def test_rename_non_uploadable_partial_marks_completed_and_cleans_staging(io, tmp_path, monkeypatch):
    staged = tmp_path / "metadata.partial"
    staged.write_bytes(b"metadata")
    doc_id = ObjectId()
    io.db.files.docs.append({
        "_id": doc_id, "type": "file", "name": "metadata.partial", "parent": "/",
        "size": 8, "local_path": str(staged),
    })

    await io.rename(PurePosixPath("/metadata.partial"), PurePosixPath("/metadata.json"))
    saved = await io.db.files.find_one({"_id": doc_id})
    assert saved["name"] == "metadata.json"
    assert saved["status"] == "completed"
    assert "local_path" not in saved
    assert not staged.exists()


@pytest.mark.asyncio
async def test_rename_missing_source_is_idempotent(io):
    await io.rename(PurePosixPath("/not-present.partial"), PurePosixPath("/not-present.mkv"))
    assert io.db.files.docs == []
    assert io.db.files.updates == []


@pytest.mark.asyncio
async def test_rename_partial_replaces_existing_destination_and_queues_new_staging_file(
    io, tmp_path, monkeypatch
):
    source_id = ObjectId()
    destination_id = ObjectId()
    source_staged = tmp_path / "incoming.partial"
    displaced_staged = tmp_path / "old-final.mkv"
    source_staged.write_bytes(b"new-video")
    displaced_staged.write_bytes(b"old-video")
    io.db.files.docs.extend(
        [
            {
                "_id": source_id,
                "type": "file",
                "name": "movie.mkv.partial",
                "parent": "/Movies",
                "size": 9,
                "local_path": str(source_staged),
                "status": "staging",
            },
            {
                "_id": destination_id,
                "type": "file",
                "name": "movie.mkv",
                "parent": "/Movies",
                "size": 9,
                "local_path": str(displaced_staged),
                "status": "staging",
            },
        ]
    )
    enqueued = []

    class Queue:
        async def put(self, item):
            enqueued.append(item)

    monkeypatch.setattr(pathio, "UPLOAD_QUEUE", Queue())

    await io.rename(
        PurePosixPath("/Movies/movie.mkv.partial"),
        PurePosixPath("/Movies/movie.mkv"),
    )

    assert not displaced_staged.exists()
    assert source_staged.read_bytes() == b"new-video"
    assert [doc["_id"] for doc in io.db.files.docs] == [source_id]
    assert io.db.files.docs[0]["name"] == "movie.mkv"
    assert io.db.files.docs[0]["parent"] == "/Movies"
    assert enqueued == [
        {
            "path": str(source_staged),
            "filename": "movie.mkv",
            "parent": "/Movies",
            "size": 9,
        }
    ]


@pytest.mark.asyncio
async def test_telegram_stream_seek_crosses_part_boundary_and_releases_bot(io, monkeypatch):
    monkeypatch.delenv("NEBULA_PLAYBACK_CACHE_ROOT", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_TOKEN", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_PORT", raising=False)
    bot = type("Bot", (), {"name": "worker-1", "_nebula_streams": 0, "_nebula_uploads": 0})()
    calls = []

    class FakeTelegramFile:
        reference_refreshed = False

        def __init__(self, tg_file, _bot, **kwargs):
            self.tg_file = tg_file
            self.message_id = kwargs["message_id"]
            self.file_id = tg_file

        async def stream(self, offset):
            calls.append((self.tg_file, self.message_id, offset))
            payload = {"part-a": b"abc", "part-b": b"def"}[self.tg_file]
            yield payload[offset:]

    monkeypatch.setattr(pathio, "File", FakeTelegramFile)
    io.tg = [bot]
    node = pathio.Node(
        "file", "joined.mkv", size=6,
        parts=[
            {"part_id": 1, "tg_file": "part-a", "tg_message": 10, "file_size": 3, "bot_index": 0},
            {"part_id": 2, "tg_file": "part-b", "tg_message": 11, "file_size": 3, "bot_index": 0},
        ],
        _id=ObjectId(),
    )
    reader = pathio.MongoDBMemoryIO(node, "rb", io.tg, io.db)
    await reader.seek(2)
    content = b"".join([chunk async for chunk in reader.iter_by_block(8)])
    assert content == b"cdef"
    assert calls == [("part-a", 10, 2), ("part-b", 11, 0)]
    assert bot._nebula_streams == 0


@pytest.mark.asyncio
async def test_telegram_stream_persists_refreshed_file_reference(io, monkeypatch):
    monkeypatch.delenv("NEBULA_PLAYBACK_CACHE_ROOT", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_TOKEN", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_PORT", raising=False)
    bot = type("Bot", (), {"name": "worker-2", "_nebula_streams": 0, "_nebula_uploads": 0})()

    class FakeTelegramFile:
        reference_refreshed = True
        file_id = "refreshed-file-id"

        def __init__(self, *_args, **_kwargs):
            pass

        async def stream(self, offset):
            assert offset == 0
            yield b"content"

    monkeypatch.setattr(pathio, "File", FakeTelegramFile)
    io.tg = [bot]
    media_id = ObjectId()
    doc = {
        "_id": media_id,
        "type": "file",
        "name": "refresh.mkv",
        "parent": "/",
        "parts": [{"part_id": 1, "tg_file": "stale-file-id", "tg_message": 7, "file_size": 7}],
    }
    io.db.files.docs.append(doc)
    node = pathio.Node("file", "refresh.mkv", size=7, parts=doc["parts"], _id=media_id)
    reader = pathio.MongoDBMemoryIO(node, "rb", io.tg, io.db)
    assert b"".join([chunk async for chunk in reader.iter_by_block(10)]) == b"content"
    query, update = io.db.files.updates[-1]
    assert query == {"_id": media_id, "parts.part_id": 1}
    assert update["$set"]["parts.$.tg_file"] == "refreshed-file-id"
    assert update["$set"]["parts.$.bot_name"] == "worker-2"
    assert doc["parts"][0]["tg_file"] == "refreshed-file-id"
    assert bot._nebula_streams == 0


def test_get_free_bytes_walks_to_existing_parent(monkeypatch, tmp_path):
    seen = []
    monkeypatch.setattr(pathio.shutil, "disk_usage", lambda p: (seen.append(p) or type("Usage", (), {"free": 1234})()))
    assert pathio.get_free_bytes(str(tmp_path / "not-created" / "deeper")) == 1234
    assert seen == [str(tmp_path)]


def test_get_free_bytes_returns_zero_when_disk_probe_fails(monkeypatch, tmp_path):
    monkeypatch.setattr(pathio.shutil, "disk_usage", lambda _p: (_ for _ in ()).throw(OSError("offline")))
    assert pathio.get_free_bytes(str(tmp_path)) == 0


def test_get_cache_dir_selects_first_disk_with_safe_reserve(monkeypatch, tmp_path):
    first, second = tmp_path / "first", tmp_path / "second"
    first.mkdir()
    second.mkdir()
    usage = {
        str(first): (100 * 1024**3, 90 * 1024**3),
        str(second): (100 * 1024**3, 30 * 1024**3),
    }
    monkeypatch.setattr(pathio, "CACHE_DIRS", [str(first), str(second)])
    monkeypatch.setattr(pathio.shutil, "disk_usage", lambda p: type("Usage", (), {"total": usage[p][0], "free": usage[p][1]})())
    assert pathio.get_cache_dir(required_bytes=1) == str(first)


def test_get_cache_dir_uses_staging_default_when_no_volumes_are_configured(monkeypatch, tmp_path):
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(pathio, "CACHE_DIRS", [])

    assert pathio.get_cache_dir() == str(tmp_path / "staging")


def test_get_cache_dir_skips_volume_when_disk_probe_fails(monkeypatch, tmp_path):
    unavailable, available = tmp_path / "unavailable", tmp_path / "available"
    unavailable.mkdir()
    available.mkdir()
    monkeypatch.setattr(pathio, "CACHE_DIRS", [str(unavailable), str(available)])

    def disk_usage(path):
        if path == str(unavailable):
            raise OSError("volume unavailable")
        return type("Usage", (), {"total": 100 * 1024**3, "free": 90 * 1024**3})()

    monkeypatch.setattr(pathio.shutil, "disk_usage", disk_usage)

    assert pathio.get_cache_dir() == str(available)


def test_get_cache_dir_falls_back_to_disk_with_most_free_space(monkeypatch, tmp_path):
    first, second = tmp_path / "first", tmp_path / "second"
    first.mkdir()
    second.mkdir()
    usage = {
        str(first): (100 * 1024**3, 2 * 1024**3),
        str(second): (100 * 1024**3, 4 * 1024**3),
    }
    monkeypatch.setattr(pathio, "CACHE_DIRS", [str(first), str(second)])
    monkeypatch.setattr(pathio.shutil, "disk_usage", lambda p: type("Usage", (), {"total": usage[p][0], "free": usage[p][1]})())
    assert pathio.get_cache_dir() == str(second)


@pytest.mark.asyncio
async def test_playback_cache_lease_is_disabled_without_server_configuration(monkeypatch):
    monkeypatch.delenv("NEBULA_CACHE_LEASE_PORT", raising=False)
    monkeypatch.delenv("NEBULA_CACHE_LEASE_TOKEN", raising=False)
    monkeypatch.setattr(pathio.asyncio, "open_connection", lambda *_args: pytest.fail("lease server is disabled"))

    async with pathio._playback_cache_lease("media-1") as acquired:
        assert acquired is False


@pytest.mark.asyncio
async def test_playback_cache_lease_acquires_and_releases_active_media(monkeypatch):
    class Reader:
        def __init__(self):
            self.responses = iter((b'{"ok":true,"leaseId":"lease-1"}\n', b'{"ok":true}\n'))

        async def readline(self):
            return next(self.responses)

    class Writer:
        def __init__(self):
            self.messages = []
            self.closed = False

        def write(self, message):
            self.messages.append(message)

        async def drain(self):
            return None

        def close(self):
            self.closed = True

        async def wait_closed(self):
            return None

    reader, writer = Reader(), Writer()
    opened = []

    async def open_connection(host, port):
        opened.append((host, port))
        return reader, writer

    monkeypatch.setenv("NEBULA_CACHE_LEASE_PORT", "45123")
    monkeypatch.setenv("NEBULA_CACHE_LEASE_TOKEN", "secret-token")
    monkeypatch.setattr(pathio.asyncio, "open_connection", open_connection)

    async with pathio._playback_cache_lease("media-1") as acquired:
        assert acquired is True

    assert opened == [("127.0.0.1", 45123)]
    assert [json.loads(message) for message in writer.messages] == [
        {"action": "acquire", "mediaId": "media-1", "token": "secret-token"},
        {"action": "release", "leaseId": "lease-1", "token": "secret-token"},
    ]
    assert writer.closed


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("failure", "response"),
    [
        (None, b'{"ok":false}\n'),
        (None, b"not-json\n"),
        (OSError("lease service unavailable"), None),
    ],
)
async def test_playback_cache_lease_fails_open_when_acquire_cannot_be_confirmed(
    monkeypatch, failure, response
):
    class Reader:
        async def readline(self):
            return response

    class Writer:
        def __init__(self):
            self.closed = False

        def write(self, _message):
            pass

        async def drain(self):
            pass

        def close(self):
            self.closed = True

        async def wait_closed(self):
            pass

    writer = Writer()

    async def open_connection(*_args):
        if failure:
            raise failure
        return Reader(), writer

    monkeypatch.setenv("NEBULA_CACHE_LEASE_PORT", "45123")
    monkeypatch.setenv("NEBULA_CACHE_LEASE_TOKEN", "secret-token")
    monkeypatch.setattr(pathio.asyncio, "open_connection", open_connection)

    async with pathio._playback_cache_lease("media-1") as acquired:
        assert acquired is False

    if not failure:
        assert writer.closed


def test_resolve_part_bot_handles_preferred_name_index_fallback_and_empty_pool():
    bot_a = type("Bot", (), {"name": "alpha"})()
    bot_b = type("Bot", (), {"name": "beta"})()
    bots = [bot_a, bot_b]

    assert pathio.resolve_part_bot({"bot_name": "beta", "bot_index": 0}, bots) is bot_b
    assert pathio.resolve_part_bot({"bot_name": "missing", "bot_index": 3}, bots) is bot_b
    assert pathio.resolve_part_bot({}, []) is None
    assert pathio.resolve_part_bot({}, bot_a) is bot_a


def test_resolve_part_bots_prioritizes_named_bot_and_deduplicates_candidates():
    bots = [type("Bot", (), {"name": name})() for name in ("alpha", "beta", "gamma")]

    named = pathio.resolve_part_bots({"bot_name": "gamma"}, bots)
    fallback = pathio.resolve_part_bots({"bot_name": "missing", "bot_index": 1}, bots)

    assert named[0] is bots[2]
    assert named[1] is bots[0]
    assert len(named) == len(set(named)) == 3
    assert fallback[0] is bots[1]
    assert fallback[1] is bots[2]
    assert len(fallback) == len(set(fallback)) == 3
    assert pathio.resolve_part_bots({}, None) == []
    assert pathio.resolve_part_bots({}, bots[0]) == [bots[0]]


@pytest.mark.asyncio
async def test_reserve_free_stream_bot_skips_busy_workers_and_waits_for_release(monkeypatch):
    busy = type("Bot", (), {"_nebula_uploads": 1, "_nebula_streams": 0})()
    available = type("Bot", (), {"_nebula_uploads": 0, "_nebula_streams": 0})()
    selected = await pathio.reserve_free_stream_bot([busy, available])
    assert selected is available
    assert available._nebula_streams == 1

    busy._nebula_uploads = "invalid-counter"
    busy._nebula_streams = 1
    released = []

    async def release_after_wait(_delay):
        released.append(True)
        busy._nebula_streams = 0

    monkeypatch.setattr(pathio, "asleep", release_after_wait)
    selected_after_wait = await pathio.reserve_free_stream_bot([busy])

    assert selected_after_wait is busy
    assert released == [True]
    assert busy._nebula_streams == 1
    assert await pathio.reserve_free_stream_bot([]) is None
