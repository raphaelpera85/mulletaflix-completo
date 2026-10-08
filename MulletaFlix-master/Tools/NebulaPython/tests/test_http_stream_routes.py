from __future__ import annotations

import asyncio
import importlib
from bson import ObjectId

import pytest

main = importlib.import_module("main")


class Writer:
    def __init__(self):
        self.data = bytearray()
        self.closed = False

    def write(self, data):
        self.data.extend(data)

    async def drain(self):
        return None

    def close(self):
        self.closed = True

    async def wait_closed(self):
        return None


class Cursor:
    def __init__(self, docs):
        self.docs = docs
        self.calls = []

    def hint(self, value):
        self.calls.append(("hint", value))
        return self

    def sort(self, *args):
        self.calls.append(("sort", args))
        return self

    def limit(self, value):
        self.calls.append(("limit", value))
        return self

    def __aiter__(self):
        self.iterator = iter(self.docs)
        return self

    async def __anext__(self):
        try:
            return next(self.iterator)
        except StopIteration:
            raise StopAsyncIteration


class Files:
    def __init__(self, docs=None, doc=None):
        self.docs = docs or []
        self.doc = doc
        self.find_calls = []

    def find(self, criteria, projection=None):
        self.find_calls.append((criteria, projection))
        self.cursor = Cursor(self.docs)
        return self.cursor

    async def find_one(self, query, projection=None):
        self.find_calls.append((query, projection))
        if self.doc and query.get("_id") == self.doc.get("_id"):
            return self.doc
        return None


class Mongo:
    def __init__(self, docs=None, doc=None):
        self.files = Files(docs, doc)


def response_body(writer):
    return bytes(writer.data).partition(b"\r\n\r\n")[2]


@pytest.mark.asyncio
async def test_list_completed_files_applies_search_hint_and_safe_projection():
    object_id = ObjectId()
    mongo = Mongo([{"_id": object_id, "name": "Mídia.mkv", "parent": "/raphael/Series", "size": 8, "uploaded_at": 123, "secret": "do-not-return"}])

    results = await main.list_completed_files(mongo, "  MÍDIA  ", "900")

    criteria, projection = mongo.files.find_calls[0]
    assert criteria["status"] == "completed"
    assert criteria["search_name"] == {"$gte": "mídia", "$lt": "mídia\uffff"}
    assert "secret" not in projection
    assert mongo.files.cursor.calls == [
        ("hint", "type_1_status_1_search_name_1_uploaded_at_-1"),
        ("sort", ("search_name", 1)),
        ("limit", 500),
    ]
    assert results == [{
        "id": str(object_id),
        "name": "Mídia.mkv",
        "parent": "/raphael/Series",
        "path": "/raphael/Series/Mídia.mkv",
        "size": 8,
        "uploaded_at": 123,
        "stream": f"/stream?id={object_id}",
        "play": f"/play?id={object_id}",
    }]


@pytest.mark.asyncio
async def test_list_completed_files_clamps_invalid_limit_and_sorts_recent_first():
    mongo = Mongo()
    assert await main.list_completed_files(mongo, "", "0") == []
    criteria, _ = mongo.files.find_calls[0]
    assert "search_name" not in criteria
    assert mongo.files.cursor.calls == [("sort", ("uploaded_at", -1)), ("limit", 1)]


@pytest.mark.asyncio
async def test_http_index_escapes_query_and_media_path():
    mongo = Mongo([{"_id": ObjectId(), "name": "<img src=x>.mkv", "parent": "/lib/<script>", "size": 10}])
    writer = Writer()

    await main.http_index(writer, mongo, '"<script>alert(1)</script>', head_only=False)

    body = response_body(writer).decode("utf-8")
    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert '&quot;&lt;script&gt;alert(1)&lt;/script&gt;' in body
    assert "&lt;script&gt;" in body
    assert "<script>alert(1)</script>" not in body


@pytest.mark.asyncio
@pytest.mark.parametrize(("filename", "expected_path"), [("movie.mp4", "/stream?id="), ("movie.mkv", "/transcode?id=")])
async def test_http_player_selects_direct_or_transcode_and_escapes_title(filename, expected_path):
    object_id = ObjectId()
    mongo = Mongo(doc={"_id": object_id, "status": "completed", "name": '<title "bad">' + filename})
    writer = Writer()

    await main.http_player(writer, mongo, str(object_id))

    body = response_body(writer).decode("utf-8")
    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert expected_path + str(object_id) in body
    assert "&lt;title &quot;bad&quot;&gt;" in body
    assert '<title "bad">' not in body


@pytest.mark.asyncio
async def test_http_player_returns_head_not_found_without_body():
    writer = Writer()
    await main.http_player(writer, Mongo(), str(ObjectId()), head_only=True)
    assert bytes(writer.data).startswith(b"HTTP/1.1 404 Not Found")
    assert response_body(writer) == b""


@pytest.mark.asyncio
async def test_stream_completed_file_gets_requested_range_and_writes_only_selected_bytes(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={
        "_id": object_id,
        "type": "file",
        "status": "completed",
        "parts": [{"part_id": 0}],
        "size": 6,
        "name": "movie.mp4",
    })
    reader_args = []

    class MemoryReader:
        def __init__(self, *args):
            reader_args.extend(args)

        async def seek(self, offset):
            self.offset = offset

        async def iter_by_block(self, _block_size):
            yield b"abcdef"[self.offset :]

    monkeypatch.setattr(main, "MongoDBMemoryIO", MemoryReader)
    writer = Writer()

    await main.stream_completed_file(writer, mongo, [], str(object_id), "bytes=2-4")

    assert bytes(writer.data).startswith(b"HTTP/1.1 206 Partial Content")
    assert b"Content-Range: bytes 2-4/6" in writer.data
    assert response_body(writer) == b"cde"
    assert reader_args[0].name == "movie.mp4"


@pytest.mark.asyncio
async def test_stream_completed_file_head_does_not_open_media_reader(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={
        "_id": object_id,
        "type": "file",
        "status": "completed",
        "parts": [{"part_id": 0}],
        "size": 6,
        "name": "movie.mp4",
    })
    monkeypatch.setattr(main, "MongoDBMemoryIO", lambda *_args: pytest.fail("HEAD must not open media"))
    writer = Writer()

    await main.stream_completed_file(writer, mongo, [], str(object_id), "bytes=2-4", head_only=True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 206 Partial Content")
    assert b"Content-Length: 3" in writer.data
    assert response_body(writer) == b""


@pytest.mark.asyncio
async def test_stream_completed_file_resolves_legacy_virtual_path(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={
        "_id": object_id,
        "type": "file",
        "status": "completed",
        "parts": [{"part_id": 0}],
        "size": 3,
        "name": "episode.mkv",
    })
    looked_up = []

    class PathIO:
        async def get_node(self, path):
            looked_up.append(path)
            return type("ResolvedNode", (), {"id": object_id})()

    class Reader:
        def __init__(self, node, *_args):
            assert node.id == object_id

        async def seek(self, offset):
            assert offset == 0

        async def iter_by_block(self, _block_size):
            yield b"abc"

    monkeypatch.setattr(main, "MongoDBPathIO", PathIO)
    monkeypatch.setattr(main, "MongoDBMemoryIO", Reader)
    writer = Writer()

    await main.stream_completed_file(writer, mongo, [], "raphael/Series/episode.mkv", None)

    assert looked_up == [main.PurePosixPath("/raphael/Series/episode.mkv")]
    assert mongo.files.find_calls[0][0]["_id"] == object_id
    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert response_body(writer) == b"abc"


@pytest.mark.asyncio
async def test_stream_completed_file_clamps_traversal_path_to_root(monkeypatch):
    looked_up = []

    class PathIO:
        async def get_node(self, path):
            looked_up.append(path)
            return None

    monkeypatch.setattr(main, "MongoDBPathIO", PathIO)
    mongo = Mongo()
    writer = Writer()

    await main.stream_completed_file(writer, mongo, [], "../private/secret.mkv", None)

    assert looked_up == [main.PurePosixPath("/")]
    assert mongo.files.find_calls == []
    assert bytes(writer.data).startswith(b"HTTP/1.1 404 Not Found")


@pytest.mark.asyncio
async def test_stream_completed_file_rejects_empty_media_before_opening_reader(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={
        "_id": object_id,
        "type": "file",
        "status": "completed",
        "parts": [{"part_id": 0}],
        "size": 0,
        "name": "empty.mkv",
    })
    monkeypatch.setattr(main, "MongoDBMemoryIO", lambda *_args: pytest.fail("empty media must not open"))
    writer = Writer()

    await main.stream_completed_file(writer, mongo, [], str(object_id), None)

    assert bytes(writer.data).startswith(b"HTTP/1.1 404 Not Found")
    assert response_body(writer) == b"arquivo vazio"


@pytest.mark.asyncio
async def test_transcode_head_validates_completed_file_without_spawning_process(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={"_id": object_id, "status": "completed", "name": "movie.mkv"})
    monkeypatch.setattr(main.asyncio, "create_subprocess_exec", lambda *_a, **_kw: pytest.fail("HEAD must not spawn ffmpeg"))
    writer = Writer()

    await main.transcode_completed_file(writer, mongo, str(object_id), head_only=True)

    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK")
    assert b"Content-Type: video/mp4" in writer.data
    assert response_body(writer) == b""


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("file_id", "doc", "head_only", "status", "body"),
    [
        ("invalid-id", None, False, b"HTTP/1.1 400 Bad Request", b"id invalido"),
        ("invalid-id", None, True, b"HTTP/1.1 400 Bad Request", b""),
        (str(ObjectId()), None, False, b"HTTP/1.1 404 Not Found", b"arquivo nao encontrado"),
        (str(ObjectId()), None, True, b"HTTP/1.1 404 Not Found", b""),
    ],
)
async def test_transcode_rejects_invalid_or_missing_completed_media_without_spawning(
    monkeypatch, file_id, doc, head_only, status, body
):
    mongo = Mongo(doc=doc)
    monkeypatch.setattr(
        main.asyncio,
        "create_subprocess_exec",
        lambda *_args, **_kwargs: pytest.fail("invalid or missing media must not spawn ffmpeg"),
    )
    writer = Writer()

    await main.transcode_completed_file(writer, mongo, file_id, head_only=head_only)

    assert bytes(writer.data).startswith(status)
    assert response_body(writer) == body
    if file_id == "invalid-id":
        assert mongo.files.find_calls == []
    else:
        assert mongo.files.find_calls[0][0]["status"] == "completed"


@pytest.mark.asyncio
async def test_transcode_mkv_streams_chunked_output_with_copy_codecs(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={"_id": object_id, "status": "completed", "name": "movie.mkv"})
    calls = []

    class Process:
        returncode = 0
        terminated = False
        waited = False

        class Stdout:
            chunks = iter([b"fragment", b""])

            async def read(self, _size):
                return next(self.chunks)

        stdout = Stdout()

        def terminate(self):
            self.terminated = True

        async def wait(self):
            self.waited = True

    process = Process()

    async def create_process(*args, **kwargs):
        calls.append((args, kwargs))
        return process

    monkeypatch.setattr(main.asyncio, "create_subprocess_exec", create_process)
    monkeypatch.setattr(main, "STREAM_PORT", 19091)
    writer = Writer()

    await main.transcode_completed_file(writer, mongo, str(object_id))

    args, options = calls[0]
    assert args[:2] == ("ffmpeg", "-loglevel")
    assert f"http://127.0.0.1:19091/stream?id={object_id}" in args
    assert args[args.index("-map") : args.index("-movflags")] == (
        "-map", "0:v:0", "-map", "0:a:0?", "-c", "copy", "-sn"
    )
    assert options["stdout"] == main.asyncio.subprocess.PIPE
    assert options["stderr"] == main.asyncio.subprocess.DEVNULL
    assert bytes(writer.data).startswith(b"HTTP/1.1 200 OK\r\nContent-Type: video/mp4")
    assert response_body(writer) == b"8\r\nfragment\r\n0\r\n\r\n"
    assert process.waited
    assert not process.terminated


@pytest.mark.asyncio
async def test_transcode_non_mkv_selects_live_video_and_audio_encoders(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={"_id": object_id, "status": "completed", "name": "movie.avi"})
    calls = []

    class Process:
        returncode = 0

        class Stdout:
            async def read(self, _size):
                return b""

        stdout = Stdout()

        def terminate(self):
            pytest.fail("completed process must not be terminated")

        async def wait(self):
            return None

    async def create_process(*args, **_kwargs):
        calls.append(args)
        return Process()

    monkeypatch.setattr(main.asyncio, "create_subprocess_exec", create_process)
    writer = Writer()

    await main.transcode_completed_file(writer, mongo, str(object_id))

    args = calls[0]
    assert args[args.index("-c:v") : args.index("-movflags")] == (
        "-c:v", "libx264", "-preset", "ultrafast", "-g", "48", "-keyint_min", "48",
        "-sc_threshold", "0", "-c:a", "aac",
    )
    assert response_body(writer) == b"0\r\n\r\n"


@pytest.mark.asyncio
async def test_transcode_terminates_and_waits_for_process_when_stdout_read_fails(monkeypatch):
    object_id = ObjectId()
    mongo = Mongo(doc={"_id": object_id, "status": "completed", "name": "movie.mkv"})

    class Process:
        returncode = None
        terminated = False
        waited = False

        class Stdout:
            async def read(self, _size):
                raise OSError("ffmpeg pipe failed")

        stdout = Stdout()

        def terminate(self):
            self.terminated = True

        async def wait(self):
            self.waited = True

    process = Process()
    monkeypatch.setattr(main.asyncio, "create_subprocess_exec", lambda *_args, **_kwargs: asyncio.sleep(0, result=process))
    writer = Writer()

    with pytest.raises(OSError, match="ffmpeg pipe failed"):
        await main.transcode_completed_file(writer, mongo, str(object_id))

    assert process.terminated
    assert process.waited
