from __future__ import annotations

import json
import os
import queue
import threading
from io import BytesIO
from pathlib import Path
from types import SimpleNamespace

import pytest

from tools import feed_ftp


def test_wait_for_disk_capacity_retries_when_volume_probe_fails(tmp_path, monkeypatch, capsys):
    target = tmp_path / "stage" / "media.mkv"
    checks = []
    sleeps = []

    def disk_usage(path):
        checks.append(Path(path))
        if len(checks) == 1:
            raise OSError("staging volume unavailable")
        return SimpleNamespace(total=100, free=80)

    def record_first_sleep(seconds):
        if not sleeps:
            sleeps.append(seconds)

    monkeypatch.setattr(feed_ftp.shutil, "disk_usage", disk_usage)
    monkeypatch.setattr(feed_ftp.time, "sleep", record_first_sleep)

    feed_ftp.wait_for_disk_capacity(target, required_bytes=10, minimum_free_percent=10)

    assert checks == [target.parent, target.parent]
    assert sleeps == [10]
    assert "download pausado" in capsys.readouterr().out


class _Response:
    def __init__(self, payload: bytes, *, headers=None, status=200):
        self._stream = BytesIO(payload)
        self.headers = headers or {}
        self.status = status

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        self._stream.close()

    def read(self, size=-1):
        return self._stream.read(size)


class _FakeFiles:
    def __init__(self, *, by_path=None, by_name=None, fail_update=False):
        self.by_path = by_path
        self.by_name = by_name
        self.fail_update = fail_update
        self.updates = []

    def find_one(self, query, _projection):
        return self.by_path if "local_path" in query else self.by_name

    def update_one(self, *args, **kwargs):
        if self.fail_update:
            raise RuntimeError("simulated Mongo update failure")
        self.updates.append((args, kwargs))


class _FakeMongoClient:
    def __init__(self, files):
        self.database = SimpleNamespace(files=files)
        self.closed = False

    def __getitem__(self, _database_name):
        return self.database

    def close(self):
        self.closed = True


def _write_strm(tmp_path: Path, content="https://cdn.example/title.mp4\n") -> Path:
    path = tmp_path / "title.strm"
    path.write_text(content, encoding="utf-8")
    return path


def test_iter_files_by_priority_orders_categories_then_titles_alphabetically(tmp_path):
    categories = [
        ("Animações", ["Zulu.mkv", "Alpha.mkv"]),
        ("Filmes", ["Zulu.mkv", "Alpha.mkv"]),
        ("Séries", ["Zulu.mkv", "Alpha.mkv"]),
        ("Doramas", ["Zulu.mkv", "Alpha.mkv"]),
        ("Novelas", ["Zulu.mkv", "Alpha.mkv"]),
        ("Porno", ["Zulu.mkv", "Alpha.mkv"]),
        ("Outros", ["Zulu.mkv", "Alpha.mkv"]),
    ]
    for category, names in categories:
        for name in names:
            path = tmp_path / category / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"media")

    actual = [
        path.relative_to(tmp_path).as_posix()
        for _source, path in feed_ftp.iter_files_by_priority(
            [tmp_path], all_files=False, exclude_dirs=set()
        )
    ]
    expected = [
        f"{category}/{name}"
        for category, names in categories
        for name in sorted(names, key=str.casefold)
    ]

    assert actual == expected


def test_materialize_strm_rejects_non_http_source_before_network(tmp_path, monkeypatch):
    source = _write_strm(tmp_path, "file:///private/title.mp4\n")
    monkeypatch.setattr(feed_ftp, "urlopen", lambda *_args, **_kwargs: pytest.fail("network must not be used"))

    with pytest.raises(ValueError, match="URL .strm invalida"):
        feed_ftp.materialize_strm(source)


def test_materialize_strm_downloads_non_range_response_atomically(tmp_path, monkeypatch):
    source = _write_strm(tmp_path)
    monkeypatch.setattr(feed_ftp.socket, "setdefaulttimeout", lambda *_args: None)
    monkeypatch.setattr(feed_ftp, "urlopen", lambda *_args, **_kwargs: _Response(b"media-bytes"))

    target = feed_ftp.materialize_strm(source)

    assert target == tmp_path / "title.mp4"
    assert target.read_bytes() == b"media-bytes"
    assert list(tmp_path.glob(".title.*.download*")) == []


def test_materialize_strm_reuses_complete_parts_when_resuming(tmp_path, monkeypatch):
    source = _write_strm(tmp_path)
    target = tmp_path / "title.mp4"
    # total=6 and three parts make each existing part exactly two bytes.
    (tmp_path / ".title.download.part0").write_bytes(b"ab")
    (tmp_path / ".title.download.part1").write_bytes(b"cd")
    (tmp_path / ".title.download.part2").write_bytes(b"ef")
    requests = []
    monkeypatch.setenv("STRM_DOWNLOAD_PARTS", "3")
    monkeypatch.setattr(feed_ftp.socket, "setdefaulttimeout", lambda *_args: None)

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        requests.append(range_header)
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"}, status=206)
        pytest.fail(f"complete part unexpectedly redownloaded: {range_header}")

    monkeypatch.setattr(feed_ftp, "urlopen", fake_urlopen)

    actual = feed_ftp.materialize_strm(source, target=target)

    assert actual == target
    assert target.read_bytes() == b"abcdef"
    assert requests == ["bytes=0-0"]
    assert list(tmp_path.glob(".title.*.download*")) == []


def test_materialize_strm_downloads_and_joins_parallel_ranges(tmp_path, monkeypatch):
    source = _write_strm(tmp_path)
    payload = b"parallel-media"
    ranges = []
    monkeypatch.setenv("STRM_DOWNLOAD_PARTS", "4")
    monkeypatch.setattr(feed_ftp.socket, "setdefaulttimeout", lambda *_args: None)

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": f"bytes 0-0/{len(payload)}"}, status=206)
        ranges.append(range_header)
        start, end = map(int, range_header.removeprefix("bytes=").split("-"))
        return _Response(
            payload[start : end + 1],
            headers={"Content-Range": f"bytes {start}-{end}/{len(payload)}"},
            status=206,
        )

    monkeypatch.setattr(feed_ftp, "urlopen", fake_urlopen)

    target = feed_ftp.materialize_strm(source)

    assert target.read_bytes() == payload
    assert len(ranges) == 4
    assert list(tmp_path.glob(".title.*.download*")) == []


@pytest.mark.parametrize("bad_content_range", ["bytes 3-5/6", "bytes 0-2/7", "invalid range"])
def test_materialize_strm_rejects_mismatched_content_range_and_preserves_existing_target(
    tmp_path, monkeypatch, bad_content_range
):
    source = _write_strm(tmp_path)
    target = tmp_path / "title.mp4"
    target.write_bytes(b"previous-good-media")
    monkeypatch.setenv("STRM_DOWNLOAD_PARTS", "2")
    monkeypatch.setenv("STRM_PART_RETRIES", "1")
    monkeypatch.setattr(feed_ftp.socket, "setdefaulttimeout", lambda *_args: None)

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"}, status=206)
        if range_header == "bytes=0-2":
            return _Response(b"def", headers={"Content-Range": bad_content_range}, status=206)
        if range_header == "bytes=3-5":
            return _Response(b"def", headers={"Content-Range": "bytes 3-5/6"}, status=206)
        pytest.fail(f"unexpected range: {range_header}")

    monkeypatch.setattr(feed_ftp, "urlopen", fake_urlopen)

    with pytest.raises(IOError, match="Content-Range"):
        feed_ftp.materialize_strm(source, overwrite=True, target=target)

    assert target.read_bytes() == b"previous-good-media"


def test_materialize_strm_rejects_partial_probe_without_content_range_and_preserves_target(
    tmp_path, monkeypatch
):
    source = _write_strm(tmp_path)
    target = tmp_path / "title.mp4"
    target.write_bytes(b"previous-good-media")
    monkeypatch.setattr(feed_ftp.socket, "setdefaulttimeout", lambda *_args: None)
    monkeypatch.setattr(feed_ftp, "urlopen", lambda *_args, **_kwargs: _Response(b"x", status=206))

    with pytest.raises(IOError, match="Content-Range"):
        feed_ftp.materialize_strm(source, overwrite=True, target=target)

    assert target.read_bytes() == b"previous-good-media"
    assert not (tmp_path / ".title.download").exists()


def test_materialize_or_reuse_strm_copies_known_materialization_without_network(tmp_path, monkeypatch):
    source = _write_strm(tmp_path)
    cached = tmp_path / "cached.mp4"
    target = tmp_path / "requested.mp4"
    cached.write_bytes(b"cached-media")
    monkeypatch.setattr(feed_ftp, "urlopen", lambda *_args, **_kwargs: pytest.fail("cache should be reused"))

    actual, url, reused = feed_ftp.materialize_or_reuse_strm(
        source, overwrite=False, known_links={"https://cdn.example/title.mp4": str(cached)}, target=target
    )

    assert actual == target
    assert url == "https://cdn.example/title.mp4"
    assert reused is True
    assert target.read_bytes() == b"cached-media"


@pytest.mark.parametrize(
    ("relative_path", "completed_movies", "completed_episodes", "expected"),
    [
        (Path("Filmes") / "Arrival (2016)" / "Arrival.mkv", {("arrival", "2016")}, set(), True),
        (Path("Filmes") / "Arrival (2016)" / "Arrival.mkv", {("arrival", "2017")}, set(), False),
        (
            Path("Series") / "The Agency" / "Season 01" / "The Agency S01E01.mkv",
            set(),
            {("the agency", 1, 1)},
            True,
        ),
    ],
)
def test_completed_destination_matches_identity_index_without_mongo(
    tmp_path, relative_path, completed_movies, completed_episodes, expected
):
    destination_root = tmp_path / "library"
    destination = destination_root / relative_path
    completed_index = (set(), set(), completed_movies, completed_episodes)

    assert feed_ftp.is_completed_destination(
        "mongodb://test", destination_root, destination, completed_index=completed_index
    ) is expected


def test_completed_destination_without_mongo_uri_returns_false(monkeypatch, tmp_path):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("empty Mongo URI must not connect"),
    )

    assert feed_ftp.is_completed_destination(
        "", tmp_path / "library", tmp_path / "library" / "Filmes" / "Arrival.mkv"
    ) is False


def test_mongo_parent_for_returns_user_root_for_destination_root(monkeypatch, tmp_path):
    monkeypatch.delenv("NEBULA_LIBRARY_USER", raising=False)
    destination_root = tmp_path / "library"

    assert feed_ftp.mongo_parent_for(destination_root, destination_root) == "/raphael"


@pytest.mark.parametrize("matched_name", ["Arrival.mkv", "Arrival.mp4"])
def test_completed_destination_matches_cached_exact_name_or_stem_without_mongo(
    monkeypatch, tmp_path, matched_name
):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("cached name hit must not query Mongo"),
    )
    destination = tmp_path / "library" / "Filmes" / "Arrival (2016)" / "Arrival.mkv"
    parent = "/raphael/filmes/arrival (2016)"
    completed_index = (
        {(parent, matched_name.casefold())} if matched_name == "Arrival.mkv" else set(),
        {(parent, Path(matched_name).stem.casefold())},
        set(),
        set(),
    )

    assert feed_ftp.is_completed_destination(
        "mongodb://test", tmp_path / "library", destination, completed_index=completed_index
    ) is True


def test_completed_destination_cached_episode_identity_falls_back_to_source_name(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("cached episode identity must not query Mongo"),
    )
    destination_root = tmp_path / "library"
    destination = destination_root / "Series/The Agency/Season 01/unknown.mkv"
    source = Path("The Agency S01E02.strm")

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination,
        src=source,
        completed_index=(set(), set(), set(), {("the agency", 1, 2)}),
    ) is True


def test_completed_destination_cached_episode_identity_matches_destination_name(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("cached episode identity must not query Mongo"),
    )
    destination_root = tmp_path / "library"
    destination = destination_root / "Series/The Agency/Season 01/The Agency S01E02.mkv"

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination,
        completed_index=(set(), set(), set(), {("the agency", 1, 2)}),
    ) is True


def test_completed_destination_cached_index_miss_returns_false_without_mongo(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("a cache miss must not query Mongo"),
    )
    destination_root = tmp_path / "library"
    destination = destination_root / "Filmes/Arrival (2016)/Arrival.mkv"

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination,
        completed_index=(set(), set(), set(), set()),
    ) is False


def test_completed_destination_cached_movie_identity_uses_stem_when_path_resolve_fails(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args: "/raphael/filmes/unknown")
    monkeypatch.setattr(
        Path,
        "resolve",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("simulated resolve failure")),
    )
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("cached movie identity must not query Mongo"),
    )
    destination = Path("Filmes/Unknown/Arrival (2016).mkv")

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        tmp_path / "library",
        destination,
        completed_index=(set(), set(), {("arrival", "2016")}, set()),
    ) is True


def test_completed_destination_direct_mongo_identity_fallback_when_path_resolve_fails(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args: "/raphael/filmes/arrival (2016)")
    monkeypatch.setattr(
        Path,
        "resolve",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("simulated resolve failure")),
    )

    class Files:
        def find(self, *_args, **_kwargs):
            return iter(())

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    destination = Path("Filmes/Arrival (2016)/movie.mkv")

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        tmp_path / "library",
        destination,
        completed_identities=({("arrival", "2016")}, set()),
    ) is True
    assert client.closed is True


@pytest.mark.parametrize(
    ("file_id", "stored_id_kind", "expected"),
    [
        ("507f1f77bcf86cd799439011", "object", True),
        ("507f1f77bcf86cd799439011", "string", True),
        ("not-an-object-id", "string", True),
        ("507f1f77bcf86cd799439011", "missing", False),
    ],
)
def test_completed_destination_resolves_strm_url_id_and_closes_client(
    tmp_path, monkeypatch, file_id, stored_id_kind, expected
):
    destination_root = tmp_path / "library"
    destination = destination_root / "Filmes" / "Arrival (2016)" / "Arrival.mkv"
    queries = []

    class Files:
        def find_one(self, query, projection):
            assert query["status"] == "completed"
            assert projection == {"_id": 1}
            queries.append(query["_id"])
            if stored_id_kind == "object" and isinstance(query["_id"], feed_ftp.ObjectId):
                return {"_id": query["_id"]}
            if stored_id_kind == "string" and query["_id"] == file_id:
                return {"_id": file_id}
            return None

    class Client:
        closed = False

        def __getitem__(self, _name):
            return SimpleNamespace(files=Files())

        def close(self):
            self.closed = True

    client = Client()
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    result = feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination,
        url=f"https://media.invalid/stream?id={file_id}",
        completed_index=(set(), set(), set(), set()),
    )

    assert result is expected
    assert client.closed is True
    if stored_id_kind == "object":
        assert len(queries) == 1 and isinstance(queries[0], feed_ftp.ObjectId)
    elif len(file_id) == 24:
        assert len(queries) == 2
        assert isinstance(queries[0], feed_ftp.ObjectId)
        assert queries[1] == file_id
    else:
        assert len(queries) == 1 and queries[0] == file_id


@pytest.mark.parametrize(
    ("relative_destination", "source_name", "records", "completed_movies", "completed_episodes", "expected"),
    [
        (Path("Filmes/Arrival (2016)/Arrival.mkv"), None, [{"name": "Arrival.mkv"}], set(), set(), True),
        (Path("Filmes/Arrival (2016)/Arrival.mkv"), None, [{"name": "Arrival.mp4"}], set(), set(), True),
        (Path("Filmes/Arrival (2016)/movie.mkv"), None, [], {("arrival", "2016")}, set(), True),
        (Path("Filmes/Unknown/Arrival (2016).mkv"), None, [], {("arrival", "2016")}, set(), True),
        (
            Path("Series/The Agency/Season 01/The Agency S01E02.mkv"),
            None,
            [],
            set(),
            {("the agency", 1, 2)},
            True,
        ),
        (
            Path("Series/The Agency/Season 01/episode.mkv"),
            "The Agency S01E02.strm",
            [],
            set(),
            {("the agency", 1, 2)},
            True,
        ),
        (Path("Filmes/Arrival (2016)/Arrival.mkv"), None, [], set(), set(), False),
    ],
)
def test_completed_destination_without_cache_queries_mongo_and_matches_identities(
    tmp_path,
    monkeypatch,
    relative_destination,
    source_name,
    records,
    completed_movies,
    completed_episodes,
    expected,
):
    destination_root = tmp_path / "library"
    destination = destination_root / relative_destination
    source = tmp_path / source_name if source_name else None
    observed_queries = []

    class Files:
        def find(self, query, projection):
            observed_queries.append((query, projection))
            return iter(records)

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    completed_identities = (completed_movies, completed_episodes)

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination,
        src=source,
        completed_identities=completed_identities,
    ) is expected
    assert observed_queries == [
        (
            {
                "parent": f"/raphael/{'/'.join(part.casefold() for part in relative_destination.parts[:-1])}",
                "status": "completed",
            },
            {"name": 1},
        )
    ]
    assert client.closed is True


def test_completed_destination_loads_media_identities_when_cache_is_not_supplied(
    tmp_path, monkeypatch
):
    destination_root = tmp_path / "library"
    destination = destination_root / "Series/The Agency/Season 01/The Agency S01E02.mkv"

    class Files:
        def find(self, *_args, **_kwargs):
            return iter(())

    client = _FakeMongoClient(Files())
    identity_calls = []

    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(
        feed_ftp,
        "completed_media_identities",
        lambda uri: (identity_calls.append(uri) or (set(), {("the agency", 1, 2)})),
    )

    assert feed_ftp.is_completed_destination(
        "mongodb://test", destination_root, destination
    ) is True
    assert identity_calls == ["mongodb://test"]
    assert client.closed is True


def test_completed_destination_returns_false_and_closes_client_when_direct_query_fails(monkeypatch, tmp_path):
    class Files:
        def find(self, *_args, **_kwargs):
            raise feed_ftp.PyMongoError("simulated completed-file query failure")

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    destination_root = tmp_path / "library"

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination_root / "Filmes" / "Arrival (2016)" / "Arrival.mkv",
    ) is False
    assert client.closed is True


def test_completed_destination_continues_to_mongo_index_when_url_parsing_fails(monkeypatch, tmp_path):
    def fail_parse(_url):
        raise ValueError("simulated malformed media URL")

    monkeypatch.setattr(feed_ftp, "urlsplit", fail_parse)
    client = _FakeMongoClient(_FakeFiles())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    destination_root = tmp_path / "library"

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination_root / "Filmes" / "Arrival (2016)" / "Arrival.mkv",
        url="not a valid URL",
    ) is False
    assert client.closed is True


def test_completed_destination_returns_false_and_closes_client_on_unexpected_mongo_error(
    monkeypatch, tmp_path
):
    client = _FakeMongoClient(_FakeFiles())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(
        feed_ftp,
        "mongo_database",
        lambda *_args: (_ for _ in ()).throw(RuntimeError("simulated unexpected error")),
    )
    destination_root = tmp_path / "library"

    assert feed_ftp.is_completed_destination(
        "mongodb://test",
        destination_root,
        destination_root / "Filmes" / "Arrival (2016)" / "Arrival.mkv",
    ) is False
    assert client.closed is True


def test_completed_media_identities_reads_movie_and_episode_records_and_closes_client(monkeypatch):
    records = [
        {"parent": "/raphael/Filmes/Arrival (2016)", "name": "Arrival.mkv", "type": "file"},
        {"parent": "/raphael/Series/The Agency/Season 01", "name": "The Agency S01E01.mkv", "type": "file"},
        {"parent": "/raphael/Other", "name": "unclassified.mkv", "type": "file"},
    ]

    class Files:
        def find(self, query, projection):
            assert query == {"type": "file", "status": "completed"}
            assert projection == {"parent": 1, "name": 1}
            return iter(records)

    class Client:
        closed = False

        def __getitem__(self, _name):
            return type("Database", (), {"files": Files()})()

        def close(self):
            self.closed = True

    client = Client()
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    movies, episodes = feed_ftp.completed_media_identities("mongodb://fake")

    assert movies == {("arrival", "2016")}
    assert episodes == {("the agency", 1, 1)}
    assert client.closed is True


@pytest.mark.parametrize("mongo_uri", ["", "mongodb://test"])
def test_completed_destination_index_skips_disabled_and_test_uris(monkeypatch, mongo_uri):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("test URI must not connect to MongoDB"),
    )

    assert feed_ftp.completed_destination_index(mongo_uri) == (set(), set(), set(), set())


def test_completed_destination_index_builds_exact_stem_movie_and_episode_indexes(monkeypatch):
    records = [
        {"parent": "/Raphael/Filmes/Arrival (2016)", "name": "Arrival.mkv", "type": "file"},
        {"parent": "/raphael/Series/The Agency/Season 01", "name": "The Agency S01E02.mkv", "type": "file"},
        {"parent": "/raphael/Other", "name": "poster.jpg", "type": "file"},
        {"parent": "/raphael/Other", "name": "", "type": "file"},
    ]

    class Files:
        def find(self, query, projection):
            assert query == {"status": "completed"}
            assert projection == {"parent": 1, "name": 1, "type": 1, "status": 1}
            return iter(records)

    class Client:
        def __init__(self):
            self.closed = False
            self.database = SimpleNamespace(files=Files())

        def __getitem__(self, _name):
            return self.database

        def close(self):
            self.closed = True

    client = Client()
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    exact, stems, movies, episodes = feed_ftp.completed_destination_index("mongodb://production-shaped-test")

    assert exact == {
        ("/raphael/filmes/arrival (2016)", "arrival.mkv"),
        ("/raphael/series/the agency/season 01", "the agency s01e02.mkv"),
        ("/raphael/other", "poster.jpg"),
    }
    assert stems == {
        ("/raphael/filmes/arrival (2016)", "arrival"),
        ("/raphael/series/the agency/season 01", "the agency s01e02"),
        ("/raphael/other", "poster"),
    }
    assert movies == {("arrival", "2016")}
    assert episodes == {("the agency", 1, 2)}
    assert client.closed


def test_completed_destination_index_returns_empty_indexes_and_closes_on_cursor_failure(monkeypatch):
    class Files:
        def find(self, *_args, **_kwargs):
            raise RuntimeError("cursor unavailable")

    class Client:
        closed = False

        def __getitem__(self, _name):
            return SimpleNamespace(files=Files())

        def close(self):
            self.closed = True

    client = Client()
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    assert feed_ftp.completed_destination_index("mongodb://production-shaped-test") == (
        set(), set(), set(), set()
    )
    assert client.closed


def test_cleanup_stale_downloads_removes_old_artifacts_but_keeps_recent_files(tmp_path, monkeypatch):
    source = tmp_path / "source"
    source.mkdir()
    stale = source / "old.mp4.partial"
    recent = source / "new.mp4.partial"
    stale.write_bytes(b"1234")
    recent.write_bytes(b"12")
    now = 1_000_000.0
    monkeypatch.setattr(feed_ftp.time, "time", lambda: now)
    os.utime(stale, (now - 100, now - 100))
    os.utime(recent, (now - 5, now - 5))

    removed, released = feed_ftp.cleanup_stale_downloads([source], max_age_seconds=30)

    assert (removed, released) == (1, 4)
    assert not stale.exists()
    assert recent.read_bytes() == b"12"


def test_load_seen_returns_empty_when_state_file_is_missing(tmp_path):
    assert feed_ftp.load_seen(tmp_path / "missing.json") == set()


@pytest.mark.parametrize("payload", ["not-json", "null", "42"])
def test_load_seen_recovers_from_corrupt_or_non_collection_json(tmp_path, payload):
    state_file = tmp_path / "state.json"
    state_file.write_text(payload, encoding="utf-8")

    assert feed_ftp.load_seen(state_file) == set()


@pytest.mark.parametrize("payload", ["not-json", "[]", "null", "42"])
def test_load_materialized_links_returns_empty_for_invalid_state(tmp_path, payload):
    state_file = tmp_path / "links.json"
    state_file.write_text(payload, encoding="utf-8")

    assert feed_ftp.load_materialized_links(state_file) == {}


def test_materialized_links_round_trip_and_sort_keys(tmp_path):
    state_file = tmp_path / "nested" / "links.json"
    links = {"z-url": "z.mp4", "á-url": "a.mp4"}

    feed_ftp.save_materialized_links(state_file, links)

    serialized = state_file.read_text(encoding="utf-8")
    assert feed_ftp.load_materialized_links(state_file) == links
    assert serialized.index('"z-url"') < serialized.index('"á-url"')


def test_load_materialized_links_converts_legacy_non_string_entries_to_strings(tmp_path):
    state_file = tmp_path / "links.json"
    state_file.write_text('{"url": 42}', encoding="utf-8")

    assert feed_ftp.load_materialized_links(state_file) == {"url": "42"}


def test_atomic_write_json_preserves_previous_state_and_cleans_temp_after_replace_failure(tmp_path, monkeypatch):
    state_file = tmp_path / "state.json"
    state_file.write_text('{"previous": true}', encoding="utf-8")
    temp_file = state_file.with_name(f".{state_file.name}.{os.getpid()}.tmp")
    original_replace = Path.replace

    def fail_state_replace(path, target):
        if path == temp_file:
            raise PermissionError("simulated atomic replace failure")
        return original_replace(path, target)

    monkeypatch.setattr(Path, "replace", fail_state_replace)

    with pytest.raises(PermissionError, match="simulated atomic replace failure"):
        feed_ftp.atomic_write_json(state_file, {"new": True})

    assert state_file.read_text(encoding="utf-8") == '{"previous": true}'
    assert not temp_file.exists()


def test_prune_completed_strm_dry_run_reports_matches_without_deleting(tmp_path, monkeypatch):
    source = tmp_path / "source"
    film_folder = source / "Filmes" / "Film 2020"
    film_folder.mkdir(parents=True)
    strm = film_folder / "Film.strm"
    strm.write_text("https://cdn.example/film.mp4", encoding="utf-8")
    _stub_completed_index(monkeypatch, movies={("film", "2020")})

    result = feed_ftp.prune_completed_strm([source], "mongodb://test", apply=False, dest_root=tmp_path / "dest")

    assert result == {"scanned": 1, "matched": 1, "folders": 1, "files": 0}
    assert strm.exists()
    assert film_folder.is_dir()


def test_prune_completed_strm_apply_removes_only_matched_leaf_folder(tmp_path, monkeypatch):
    source = tmp_path / "source"
    film_folder = source / "Filmes" / "Film 2020"
    film_folder.mkdir(parents=True)
    strm = film_folder / "Film.strm"
    strm.write_text("https://cdn.example/film.mp4", encoding="utf-8")
    unrelated = source / "Series" / "Not Completed" / "episode.strm"
    unrelated.parent.mkdir(parents=True)
    unrelated.write_text("https://cdn.example/episode.mp4", encoding="utf-8")
    _stub_completed_index(monkeypatch, movies={("film", "2020")})

    result = feed_ftp.prune_completed_strm([source], "mongodb://test", apply=True, dest_root=tmp_path / "dest")

    assert result == {"scanned": 2, "matched": 1, "folders": 1, "files": 0}
    assert not film_folder.exists()
    assert unrelated.read_text(encoding="utf-8") == "https://cdn.example/episode.mp4"


def test_prune_completed_strm_preserves_folder_with_other_media(tmp_path, monkeypatch):
    source = tmp_path / "source"
    episode_folder = source / "Series" / "Show"
    episode_folder.mkdir(parents=True)
    strm = episode_folder / "Show S01E01.strm"
    strm.write_text("https://cdn.example/episode.mkv", encoding="utf-8")
    media = episode_folder / "Show S01E01.mkv"
    media.write_bytes(b"existing media")
    _stub_completed_index(monkeypatch, episodes={("show", 1, 1)})

    result = feed_ftp.prune_completed_strm([source], "mongodb://test", apply=True, dest_root=tmp_path / "dest")

    assert result == {"scanned": 1, "matched": 1, "folders": 0, "files": 1}
    assert not strm.exists()
    assert media.read_bytes() == b"existing media"
    assert episode_folder.is_dir()


def _stub_completed_index(monkeypatch, *, movies=None, episodes=None):
    monkeypatch.setattr(
        feed_ftp,
        "completed_media_identities",
        lambda _mongo_uri: (set(movies or ()), set(episodes or ())),
    )
    monkeypatch.setattr(
        feed_ftp,
        "completed_destination_index",
        lambda _mongo_uri: (set(), set(), set(), set()),
    )


@pytest.mark.parametrize("status", ["completed", "queued", "staging", "uploading"])
def test_register_one_deduplicates_completed_and_active_local_path(tmp_path, monkeypatch, status):
    source = tmp_path / "source.mkv"
    source.write_bytes(b"media bytes")
    destination = tmp_path / "library" / "renamed.mkv"
    files = _FakeFiles(by_path={"_id": "existing-id", "status": status})
    client = _FakeMongoClient(files)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args, **_kwargs: "/library")
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    result = feed_ftp.register_one(source, destination, tmp_path, "mongodb://fake", False, True)

    assert result == feed_ftp.RegistrationResult(0, status)
    assert files.updates == [(
        ({"_id": "existing-id"}, {"$set": {"delete_source": True, "name": "renamed.mkv", "parent": "/library"}}),
        {},
    )]
    assert client.closed is True


@pytest.mark.parametrize("status", ["completed", "queued", "staging", "uploading"])
def test_register_one_deduplicates_legacy_destination_name(tmp_path, monkeypatch, status):
    source = tmp_path / "source.mkv"
    source.write_bytes(b"media bytes")
    destination = tmp_path / "library" / "title.mkv"
    files = _FakeFiles(by_name={"status": status, "local_path": "legacy-source.mkv"})
    client = _FakeMongoClient(files)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args, **_kwargs: "/library")
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    result = feed_ftp.register_one(source, destination, tmp_path, "mongodb://fake", False, True)

    assert result == feed_ftp.RegistrationResult(0, status)
    assert files.updates == [(
        ({"parent": "/library", "name": "title.mkv"}, {"$set": {"delete_source": True}}),
        {},
    )]
    assert client.closed is True


def test_ensure_nebula_metadata_creates_and_reuses_nested_directory_chain(tmp_path, monkeypatch):
    dest_root = tmp_path / "library"
    destination_parent = dest_root / "Series" / "Example" / "Season 01"
    records = {}

    class Files:
        def find_one(self, query, _projection):
            return records.get((query["parent"], query["name"]))

        def insert_one(self, document):
            records[(document["parent"], document["name"])] = document

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setenv("NEBULA_LIBRARY_USER", "media-user")
    monkeypatch.setattr(feed_ftp.time, "time", lambda: 1234)

    feed_ftp.ensure_nebula_metadata("mongodb://fake", dest_root, destination_parent)
    feed_ftp.ensure_nebula_metadata("mongodb://fake", dest_root, destination_parent)

    assert [(document["name"], document["parent"]) for document in records.values()] == [
        ("Series", "/media-user"),
        ("Example", "/media-user/Series"),
        ("Season 01", "/media-user/Series/Example"),
    ]
    assert all(document["type"] == "dir" and document["ctime"] == 1234 for document in records.values())
    assert client.closed is True


def test_ensure_nebula_metadata_returns_for_paths_outside_destination_root(tmp_path, monkeypatch):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("outside path must not connect to Mongo"),
    )

    feed_ftp.ensure_nebula_metadata(
        "mongodb://fake", tmp_path / "library", tmp_path / "unrelated" / "Season 01"
    )


def test_ensure_nebula_metadata_does_not_create_destination_root_record(tmp_path, monkeypatch):
    monkeypatch.setattr(
        feed_ftp,
        "MongoClient",
        lambda *_args, **_kwargs: pytest.fail("destination root has no parent directory record"),
    )

    dest_root = tmp_path / "library"
    feed_ftp.ensure_nebula_metadata("mongodb://fake", dest_root, dest_root)


def test_ensure_nebula_metadata_tolerates_concurrent_directory_insert(tmp_path, monkeypatch):
    dest_root = tmp_path / "library"
    destination_parent = dest_root / "Series" / "Example"
    records = {}
    raced = False

    class Files:
        def find_one(self, query, _projection):
            return records.get((query["parent"], query["name"]))

        def insert_one(self, document):
            nonlocal raced
            key = (document["parent"], document["name"])
            records[key] = document
            if not raced:
                raced = True
                raise feed_ftp.DuplicateKeyError("directory inserted concurrently")

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setenv("NEBULA_LIBRARY_USER", "media-user")

    feed_ftp.ensure_nebula_metadata("mongodb://fake", dest_root, destination_parent)

    assert set(records) == {("/media-user", "Series"), ("/media-user/Series", "Example")}
    assert client.closed is True


def test_ensure_nebula_metadata_closes_client_when_mongo_fails(tmp_path, monkeypatch):
    class Files:
        def find_one(self, *_args, **_kwargs):
            raise feed_ftp.PyMongoError("simulated query failure")

    client = _FakeMongoClient(Files())
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    feed_ftp.ensure_nebula_metadata(
        "mongodb://fake", tmp_path / "library", tmp_path / "library" / "Series"
    )

    assert client.closed is True


def test_register_one_requeues_failed_local_path_with_current_destination(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    source.write_bytes(b"media bytes")
    files = _FakeFiles(by_path={"_id": "failed-id", "status": "failed"})
    client = _FakeMongoClient(files)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args, **_kwargs: "/library")
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)
    monkeypatch.setattr(feed_ftp.time, "time", lambda: 1234)

    result = feed_ftp.register_one(source, tmp_path / "renamed.mkv", tmp_path, "mongodb://fake", False)

    assert result == feed_ftp.RegistrationResult(len(b"media bytes"))
    update_args, _kwargs = files.updates[0]
    assert update_args[0] == {"_id": "failed-id"}
    assert update_args[1]["$set"] == {
        "name": "renamed.mkv", "parent": "/library", "size": len(b"media bytes"),
        "status": "queued", "mtime": 1234, "delete_source": False,
    }
    assert client.closed is True


def test_register_one_upserts_when_no_existing_path_or_destination_name(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    source.write_bytes(b"media bytes")
    files = _FakeFiles()
    client = _FakeMongoClient(files)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args, **_kwargs: "/library")
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    result = feed_ftp.register_one(source, tmp_path / "title.mkv", tmp_path, "mongodb://fake", True)

    assert result == feed_ftp.RegistrationResult(len(b"media bytes"))
    update_args, update_kwargs = files.updates[0]
    assert update_args[0] == {"parent": "/library", "name": "title.mkv"}
    assert update_args[1]["$set"]["local_path"] == str(source)
    assert update_args[1]["$set"]["status"] == "queued"
    assert update_kwargs == {"upsert": True}
    assert client.closed is True


def test_register_one_closes_mongo_client_after_database_failure(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    source.write_bytes(b"media bytes")
    files = _FakeFiles(by_path={"_id": "existing-id", "status": "failed"}, fail_update=True)
    client = _FakeMongoClient(files)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args, **_kwargs: "/library")
    monkeypatch.setattr(feed_ftp, "MongoClient", lambda *_args, **_kwargs: client)

    with pytest.raises(RuntimeError, match="simulated Mongo update failure"):
        feed_ftp.register_one(source, tmp_path / "title.mkv", tmp_path, "mongodb://fake", False)

    assert client.closed is True


def test_upload_worker_records_success_and_releases_pending_item(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    destination = tmp_path / "destination.mkv"
    source.write_bytes(b"media")
    jobs = queue.Queue()
    jobs.put((source, destination))
    jobs.put(None)
    monkeypatch.setattr(feed_ftp, "copy_one", lambda *_args, **_kwargs: len(b"media"))
    stats = feed_ftp.Stats(queued=1)
    lock = threading.Lock()
    pending = {str(source)}
    seen = set()

    feed_ftp.worker(
        1, jobs, stats, lock, tmp_path, "mongodb://fake", False, 1, set(), threading.Lock(),
        pending, False, seen=seen, state_file=tmp_path / "seen.json",
    )

    assert (stats.copied, stats.bytes_copied, stats.failed) == (1, len(b"media"), 0)
    assert pending == set()
    assert seen == {str(source)}
    assert jobs.unfinished_tasks == 0


def test_upload_worker_records_terminal_failure_and_releases_pending_item(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    destination = tmp_path / "destination.mkv"
    source.write_bytes(b"media")
    jobs = queue.Queue()
    jobs.put((source, destination))
    jobs.put(None)

    def fail_copy(*_args, **_kwargs):
        raise OSError("destination unavailable")

    monkeypatch.setattr(feed_ftp, "copy_one", fail_copy)
    stats = feed_ftp.Stats(queued=1)
    pending = {str(source)}
    failed = set()

    feed_ftp.worker(
        1, jobs, stats, threading.Lock(), tmp_path, "mongodb://fake", False, 1,
        set(), threading.Lock(), pending, False, failed_strm=failed,
    )

    assert (stats.copied, stats.failed) == (0, 1)
    assert pending == set()
    assert failed == {str(source)}
    assert jobs.unfinished_tasks == 0


@pytest.mark.parametrize(
    ("registered_size", "existing_status", "expected_stat", "source_should_exist"),
    [
        (5, None, "copied", True),
        (0, "completed", "skipped", False),
        (0, "queued", "skipped", True),
        (0, "staging", "skipped", True),
        (0, "uploading", "skipped", True),
    ],
)
def test_upload_worker_direct_mongo_honors_registration_and_source_cleanup(
    tmp_path, monkeypatch, registered_size, existing_status, expected_stat, source_should_exist
):
    source = tmp_path / "stage" / "episode.mkv"
    source.parent.mkdir()
    source.write_bytes(b"media")
    destination = tmp_path / "library" / "episode.mkv"
    jobs = queue.Queue()
    jobs.put((source, destination))
    jobs.put(None)
    registrations = []
    monkeypatch.setattr(
        feed_ftp,
        "register_one",
        lambda *args: registrations.append(args)
        or feed_ftp.RegistrationResult(registered_size, existing_status),
    )
    stats = feed_ftp.Stats(queued=1)
    pending = {str(source)}
    seen = set()

    feed_ftp.worker(
        1, jobs, stats, threading.Lock(), tmp_path, "mongodb://fake", False, 1,
        set(), threading.Lock(), pending, True, seen=seen, delete_source=True,
    )

    assert len(registrations) == 1
    assert registrations[0] == (source, destination, tmp_path, "mongodb://fake", False, True)
    assert getattr(stats, expected_stat) == 1
    assert stats.failed == 0
    assert source.exists() is source_should_exist
    assert pending == set()
    assert seen == {str(source)}
    assert jobs.unfinished_tasks == 0


def test_upload_worker_retries_transient_copy_failure_then_records_success(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    destination = tmp_path / "destination" / "source.mkv"
    source.write_bytes(b"media")
    jobs = queue.Queue()
    jobs.put((source, destination))
    jobs.put(None)
    calls = []
    sleeps = []

    def flaky_copy(*_args, **_kwargs):
        calls.append(1)
        if len(calls) == 1:
            raise OSError("temporary destination error")
        return len(b"media")

    monkeypatch.setattr(feed_ftp, "copy_one", flaky_copy)
    monkeypatch.setattr(feed_ftp.time, "sleep", sleeps.append)
    stats = feed_ftp.Stats(queued=1)
    pending = {str(source)}
    failed = set()
    ensured_dirs = {str(destination.parent)}

    feed_ftp.worker(
        1, jobs, stats, threading.Lock(), tmp_path, "mongodb://fake", False, 2,
        ensured_dirs, threading.Lock(), pending, False, failed_strm=failed,
    )

    assert len(calls) == 2
    assert sleeps == [3]
    assert (stats.copied, stats.bytes_copied, stats.failed) == (1, len(b"media"), 0)
    assert pending == set()
    assert failed == set()
    assert jobs.unfinished_tasks == 0


def test_strm_worker_materializes_persists_link_and_enqueues_upload(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    src = source_root / "title.strm"
    src.write_text("https://cdn.example/title.mp4\n", encoding="utf-8")
    destination_root = tmp_path / "destination"
    materialized = source_root / "title.mp4"
    materialized.write_bytes(b"media")
    monkeypatch.setattr(
        feed_ftp,
        "materialize_or_reuse_strm",
        lambda *_args, **_kwargs: (materialized, "https://cdn.example/title.mp4", False),
    )
    strm_jobs = queue.Queue()
    upload_jobs = queue.Queue()
    strm_jobs.put((source_root, src))
    strm_jobs.put(None)
    pending = {str(src)}
    links = {}
    failed = set()
    seen = set()
    links_file = tmp_path / "links.json"
    state_file = tmp_path / "seen.json"

    feed_ftp.strm_worker(
        1, strm_jobs, upload_jobs, feed_ftp.Stats(), threading.Lock(), destination_root,
        False, pending, links, links_file, failed, seen=seen, state_file=state_file,
    )

    assert upload_jobs.get_nowait() == (materialized, destination_root / "title.mp4")
    assert pending == {str(materialized)}
    assert seen == {str(src)}
    assert json.loads(links_file.read_text(encoding="utf-8")) == {
        "https://cdn.example/title.mp4": str(materialized)
    }
    assert json.loads(state_file.read_text(encoding="utf-8")) == [str(src)]
    assert failed == set()
    assert strm_jobs.unfinished_tasks == 0


def test_strm_worker_marks_materialization_failure_and_releases_queue_task(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    src = _write_strm(source_root)
    monkeypatch.setattr(
        feed_ftp,
        "materialize_or_reuse_strm",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("remote unavailable")),
    )
    strm_jobs = queue.Queue()
    upload_jobs = queue.Queue()
    strm_jobs.put((source_root, src))
    strm_jobs.put(None)
    failed = set()
    pending = {str(src)}

    feed_ftp.strm_worker(
        1, strm_jobs, upload_jobs, feed_ftp.Stats(), threading.Lock(), tmp_path / "destination",
        False, pending, {}, tmp_path / "links.json", failed,
    )

    assert failed == {str(src)}
    assert pending == set()
    assert upload_jobs.empty()
    assert strm_jobs.unfinished_tasks == 0


def test_strm_worker_releases_pending_when_media_is_already_completed(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    src = _write_strm(source_root)
    monkeypatch.setattr(feed_ftp, "is_completed_destination", lambda *_args: True)
    monkeypatch.setattr(
        feed_ftp,
        "materialize_or_reuse_strm",
        lambda *_args, **_kwargs: pytest.fail("completed media must not be materialized"),
    )
    strm_jobs = queue.Queue()
    upload_jobs = queue.Queue()
    strm_jobs.put((source_root, src))
    strm_jobs.put(None)
    pending = {str(src)}
    seen = set()

    feed_ftp.strm_worker(
        1, strm_jobs, upload_jobs, feed_ftp.Stats(), threading.Lock(), tmp_path / "destination",
        False, pending, {}, tmp_path / "links.json", set(), seen=seen,
        mongo_uri="mongodb://fake",
    )

    assert pending == set()
    assert seen == {str(src)}
    assert upload_jobs.empty()
    assert strm_jobs.unfinished_tasks == 0


def test_feeder_main_processes_one_local_media_and_drains_worker_queues(tmp_path, monkeypatch):
    source_root = tmp_path / "source" / "Filmes"
    source_root.mkdir(parents=True)
    source = source_root / "Amostra (2025).mkv"
    source.write_bytes(b"media payload")
    destination_root = tmp_path / "destination"
    destination_root.mkdir()
    state_file = tmp_path / "feeder-state.json"
    stats_updates = []

    class StatsCollection:
        def update_one(self, *args, **kwargs):
            stats_updates.append((args, kwargs))

    class FakeDatabase:
        stats = StatsCollection()

    class FakeMongoClient:
        def __init__(self, *_args, **_kwargs):
            self.closed = False

        def __getitem__(self, _name):
            return FakeDatabase()

        def close(self):
            self.closed = True

    monkeypatch.setattr(
        feed_ftp.sys,
        "argv",
        [
            "feed_ftp.py", "--source", str(source_root.parent), "--dest", str(destination_root),
            "--workers", "1", "--max-downloads", "1", "--poll-seconds", "1",
            "--state-file", str(state_file),
        ],
    )
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")
    monkeypatch.setenv("STAGING_DIR", str(tmp_path))
    monkeypatch.setattr(feed_ftp, "MongoClient", FakeMongoClient)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args: None)
    monkeypatch.setattr(feed_ftp, "completed_media_identities", lambda *_args: (set(), set()))
    monkeypatch.setattr(feed_ftp, "is_completed_destination", lambda *_args: False)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args: "/Filmes")
    monkeypatch.setattr(feed_ftp, "cleanup_stale_downloads", lambda *_args: (0, 0))
    monkeypatch.setattr(feed_ftp, "get_best_staging_root", lambda **_kwargs: tmp_path)
    monkeypatch.setattr(feed_ftp.shutil, "disk_usage", lambda *_args: SimpleNamespace(free=80, total=100))

    result = feed_ftp.main()

    expected_destination = destination_root / "Filmes" / source.stem / source.name
    assert result == 0
    assert expected_destination.read_bytes() == b"media payload"
    assert len(stats_updates) == 1
    assert stats_updates[0][0][0] == {"_id": "feeder"}
    assert stats_updates[0][1]["upsert"] is True


def test_feeder_main_materializes_strm_and_drains_upload_queue(tmp_path, monkeypatch, capsys):
    source_root = tmp_path / "source"
    library = source_root / "Doramas" / "Drama"
    library.mkdir(parents=True)
    strm = library / "Drama S01E01.strm"
    strm.write_text("https://media.example/episode.mkv\n", encoding="utf-8")
    destination_root = tmp_path / "destination"
    destination_root.mkdir()
    stage_media = tmp_path / "stage" / "Drama S01E01.mkv"
    stage_media.parent.mkdir()
    stage_media.write_bytes(b"materialized episode")
    state_file = tmp_path / "feeder-state.json"
    stats_updates = []
    materialization_calls = []

    class StatsCollection:
        def update_one(self, *args, **kwargs):
            stats_updates.append((args, kwargs))

    class FakeDatabase:
        stats = StatsCollection()

    class FakeMongoClient:
        def __init__(self, *_args, **_kwargs):
            pass

        def __getitem__(self, _name):
            return FakeDatabase()

        def close(self):
            return None

    def materialize(_src, *, overwrite=False, known_links=None, target=None):
        materialization_calls.append((_src, overwrite, target))
        return stage_media, "https://media.example/episode.mkv", False

    monkeypatch.setattr(
        feed_ftp.sys,
        "argv",
        [
            "feed_ftp.py", "--source", str(source_root), "--dest", str(destination_root),
            "--workers", "1", "--max-downloads", "1", "--poll-seconds", "1",
            "--state-file", str(state_file),
        ],
    )
    monkeypatch.setenv("MONGODB", "mongodb://fake")
    monkeypatch.setenv("MONGO_DATABASE", "nebula-test")
    monkeypatch.setenv("STAGING_DIR", str(tmp_path / "stage"))
    monkeypatch.setattr(feed_ftp, "MongoClient", FakeMongoClient)
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args: None)
    monkeypatch.setattr(feed_ftp, "completed_media_identities", lambda *_args: (set(), set()))
    monkeypatch.setattr(feed_ftp, "is_completed_destination", lambda *_args: False)
    monkeypatch.setattr(feed_ftp, "mongo_parent_for", lambda *_args: "/Doramas/Drama")
    monkeypatch.setattr(feed_ftp, "cleanup_stale_downloads", lambda *_args: (0, 0))
    monkeypatch.setattr(feed_ftp, "get_best_staging_root", lambda **_kwargs: tmp_path / "stage")
    disk_checks = []
    sleeps = []

    def disk_usage(_path):
        disk_checks.append(_path)
        if len(disk_checks) == 1:
            raise OSError("staging volume unavailable")
        return SimpleNamespace(free=80, total=100)

    def record_first_sleep(seconds):
        if not sleeps:
            sleeps.append(seconds)

    monkeypatch.setattr(feed_ftp.shutil, "disk_usage", disk_usage)
    monkeypatch.setattr(feed_ftp.time, "sleep", record_first_sleep)
    monkeypatch.setattr(feed_ftp, "remote_content_size", lambda *_args: None)
    monkeypatch.setattr(feed_ftp, "materialize_or_reuse_strm", materialize)

    result = feed_ftp.main()

    expected_destination = destination_root / "Doramas" / "Drama" / "Drama S01E01.mkv"
    assert result == 0
    assert expected_destination.read_bytes() == b"materialized episode"
    assert materialization_calls == [(strm, False, None)]
    assert sleeps == [1]
    assert len(disk_checks) >= 2
    assert len(stats_updates) == 1
    assert stats_updates[0][0][0] == {"_id": "feeder"}
    assert "Finalizado: fila=1 copiados=1 ignorados=0 falhas=0" in capsys.readouterr().out
