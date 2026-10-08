from __future__ import annotations

import os
from pathlib import Path
from types import SimpleNamespace

import pytest
from bson import ObjectId
from pymongo.errors import OperationFailure

import main as main_module

from main import (
    classify_media_type,
    doc_download_timestamp,
    extract_media_year,
    get_contiguous_uploaded_parts,
    get_media_category_priority,
    get_small_upload_worker_count,
    get_upload_worker_count,
    get_required_config,
    has_telegram_parts,
    is_loopback_host,
    is_staging_path,
    library_category,
    next_upload_bot_index,
    queue_identity,
    resolve_local_path,
    resolve_media_parent,
    resolve_parent_to_path,
    safe_remove_staging_file,
    series_parent_from_filename,
    setup_database_indexes,
)


@pytest.mark.parametrize(
    ("parent", "expected"),
    [
        ("/raphael/Filmes", "filmes"),
        ("raphael/Animações", "animações"),
        ("/raphael/Animacoes", "animacoes"),
        ("/raphael/Series/Drama", "series"),
        ("/raphael/Doramas", "doramas"),
        ("/raphael/Novelas", "novelas"),
        ("/raphael/Porno", "porno"),
        ("/raphael/outras", None),
        ("/raphael", None),
        (None, None),
    ],
)
def test_library_category_extracts_only_supported_library_roots(parent, expected):
    assert library_category(parent) == expected


@pytest.mark.parametrize(
    ("parent", "filename", "expected"),
    [
        ("/raphael/Series", "Drama.mkv", "SERIE"),
        ("/raphael/Animações", "Anime S01E02.mkv", "SERIE"),
        ("/raphael/Porno", "video.mp4", "PORNO"),
        ("/raphael/Filmes", "Adulto S01E01.mp4", "PORNO"),
        ("/raphael/Filmes", "Movie S01E01.mkv", "SERIE"),
        ("/raphael/Filmes", "Season of Change.mkv", "FILME"),
    ],
)
def test_classify_media_type_uses_library_root_and_episode_identity(parent, filename, expected):
    assert classify_media_type(parent, filename) == expected


@pytest.mark.parametrize(
    ("name", "parent", "expected"),
    [
        ("Arrival (2016).mkv", "", 2016),
        ("Arrival [2017].mkv", "", 2017),
        ("Arrival.2018.1080p.mkv", "", 2018),
        ("No year.mkv", "Film (1999)", 1999),
        ("No year.mkv", "", 0),
    ],
)
def test_extract_media_year_from_filename_then_parent(name, parent, expected):
    assert extract_media_year(name, parent) == expected


def test_uploaded_parts_only_returns_contiguous_valid_prefix():
    parts = [
        {"part_id": 2, "tg_file": "third", "file_size": 10},
        {"part_id": 0, "tg_file": "first", "file_size": 10},
        {"part_id": 1, "tg_file": "second", "file_size": 10},
        {"part_id": 3, "tg_file": "fourth", "file_size": 0},
    ]

    assert [part["part_id"] for part in get_contiguous_uploaded_parts(parts)] == [0, 1, 2]
    assert get_contiguous_uploaded_parts(parts, total_parts=2) == [
        {"part_id": 0, "tg_file": "first", "file_size": 10},
        {"part_id": 1, "tg_file": "second", "file_size": 10},
    ]


@pytest.mark.parametrize(
    ("doc", "expected"),
    [
        ({"tg_file_id": "legacy"}, True),
        ({"parts": [{"file_id": "old"}]}, True),
        ({"parts": [{"tg_message": 42}]}, True),
        ({"parts": [{"file_size": 4}]}, False),
        ({}, False),
    ],
)
def test_has_telegram_parts_detects_legacy_and_part_level_identifiers(doc, expected):
    assert has_telegram_parts(doc) is expected


def test_worker_counts_respect_bot_and_configuration_limits(monkeypatch):
    monkeypatch.setattr("main.PART_WORKERS_PER_FILE", 2)
    monkeypatch.setattr("main.UPLOAD_CONCURRENCY", 8)
    monkeypatch.setattr("main.MAX_WORKERS", 4)
    monkeypatch.setattr("main.LARGE_WORKERS_CONFIG", 3)
    assert get_upload_worker_count(1) == 1
    assert get_upload_worker_count(8) == 4
    assert get_upload_worker_count(0) == 1
    monkeypatch.setenv("SMALL_WORKERS", "20")
    assert get_small_upload_worker_count(3) == 3
    monkeypatch.setenv("SMALL_WORKERS", "bad")
    with pytest.raises(ValueError):
        get_small_upload_worker_count(3)


def test_bot_rotation_wraps_and_empty_pool_is_safe(monkeypatch):
    monkeypatch.setattr("main.UPLOAD_BOT_CURSOR", 0)
    assert [next_upload_bot_index(3) for _ in range(5)] == [0, 1, 2, 0, 1]
    assert next_upload_bot_index(0) == 0


@pytest.mark.parametrize(
    ("host", "expected"),
    [("localhost", True), ("LOCALHOST", True), ("127.0.0.1", True), ("::1", True), ("203.0.113.4", False), ("invalid", False)],
)
def test_loopback_host_recognition(host, expected):
    assert is_loopback_host(host) is expected


def test_category_priority_keeps_current_order_and_filename_fallback():
    assert get_media_category_priority("/user/Filmes", "title.mkv") == 0
    assert get_media_category_priority("/user/other", "xxx scene.mp4") == 1
    assert get_media_category_priority("/user/other", "show S01E01.mkv") == 2
    assert get_media_category_priority("/user/other", "documentary.mkv") == 3


def test_doc_download_timestamp_uses_metadata_then_file_mtime(tmp_path):
    path = tmp_path / "media.mkv"
    path.write_bytes(b"data")
    os.utime(path, (1234, 1234))
    assert doc_download_timestamp({"mtime": 20, "ctime": 10, "local_path": str(path)}) == 20
    assert doc_download_timestamp({"ctime": 10, "local_path": str(path)}) == 10
    assert doc_download_timestamp({"local_path": str(path)}) == 1234
    assert doc_download_timestamp({"local_path": str(tmp_path / "missing")}) == 0


def test_staging_path_uses_path_boundary_and_safe_removal(tmp_path, monkeypatch):
    stage = tmp_path / "stage"
    nested = stage / "movie" / "media.mkv"
    nested.parent.mkdir(parents=True)
    nested.write_bytes(b"media")
    outside = tmp_path / "stage-copy" / "keep.mkv"
    outside.parent.mkdir()
    outside.write_bytes(b"keep")
    monkeypatch.setattr("main.STAGING_DIRS", [str(stage)])

    assert is_staging_path(nested)
    assert not is_staging_path(outside)
    safe_remove_staging_file(outside)
    assert outside.exists()
    safe_remove_staging_file(nested)
    assert not nested.exists()


@pytest.mark.asyncio
async def test_resolve_local_path_recovers_file_from_stage_and_persists_mapping(tmp_path, monkeypatch):
    stage = tmp_path / "stage"
    candidate = stage / "strm" / "episode.mkv"
    candidate.parent.mkdir(parents=True)
    candidate.write_bytes(b"media")
    monkeypatch.setattr("main.STAGING_DIRS", [str(stage)])
    updates = []

    class Files:
        async def update_one(self, query, update):
            updates.append((query, update))

    mongo = SimpleNamespace(files=Files())
    doc = {"_id": "file-id", "name": "episode.mkv", "local_path": "Z:/NebulaStage/episode.mkv"}

    assert await resolve_local_path(mongo, doc) == str(candidate)
    assert doc["local_path"] == str(candidate)
    assert updates == [({"_id": "file-id"}, {"$set": {"local_path": str(candidate)}})]


@pytest.mark.asyncio
async def test_resolve_parent_to_path_handles_strings_missing_parents_and_cycles():
    assert await resolve_parent_to_path(None, "/", "/raphael") == "/raphael"
    assert await resolve_parent_to_path(None, " raphael/Series ", "/raphael") == "/raphael/Series"
    assert await resolve_parent_to_path(None, None, "/raphael") == "/raphael/Filmes"

    first, second = ObjectId(), ObjectId()

    class Files:
        async def find_one(self, query):
            return {
                first: {"_id": first, "name": "Series", "parent": second},
                second: {"_id": second, "name": "raphael", "parent": None},
            }.get(query["_id"])

    mongo = SimpleNamespace(files=Files())
    assert await resolve_parent_to_path(mongo, first, "/raphael") == "/raphael/Series"
    assert await resolve_parent_to_path(mongo, ObjectId(), "/raphael") == "/raphael/Filmes"


def test_series_parent_from_filename_normalizes_name_and_season():
    assert series_parent_from_filename("/raphael", "My.Show_S01E02.mkv") == "/raphael/Series/My Show/Season 01"
    assert series_parent_from_filename("/raphael", "movie.mkv") is None


@pytest.mark.asyncio
async def test_resolve_media_parent_preserves_authoritative_library_and_routes_episodes(monkeypatch):
    monkeypatch.setenv("NEBULA_LIBRARY_USER", "raphael")

    class Files:
        async def find(self, query):
            raise AssertionError(f"database lookup should not be needed: {query}")

    mongo = SimpleNamespace(files=Files())
    assert await resolve_media_parent(mongo, "/raphael/Doramas", "Drama S01E01.mkv") == "/raphael/Doramas"
    assert await resolve_media_parent(mongo, "/raphael/staging", "Drama S02E03.mkv") == "/raphael/Series/Drama/Season 02"
    assert await resolve_media_parent(mongo, "/raphael/Series/Drama", "readme.txt") == "/raphael/Series/Drama"


def test_get_required_config_reads_supported_bot_token_variables(monkeypatch):
    monkeypatch.setattr(main_module, "PASSIVE_PORTS_ERROR", None)
    monkeypatch.setattr(main_module, "STREAM_HOST", "127.0.0.1")
    monkeypatch.setattr(main_module, "STREAM_TOKEN", "")
    monkeypatch.setenv("API_ID", "12345")
    monkeypatch.setenv("API_HASH", "api-hash")
    monkeypatch.setenv("MONGODB", "mongodb://local.test")
    monkeypatch.setenv("CHAT_ID", "-100123")
    monkeypatch.setenv("BOT_TOKENS", " token-one , ,token-two ")
    monkeypatch.delenv("BOT_TOKEN", raising=False)

    assert get_required_config() == (12345, "api-hash", ["token-one", "token-two"])


@pytest.mark.parametrize(
    ("missing", "expected"),
    [
        ("MONGODB", "MONGODB"),
        ("CHAT_ID", "CHAT_ID"),
        ("BOT_TOKENS", "BOT_TOKENS"),
    ],
)
def test_get_required_config_names_missing_values(monkeypatch, missing, expected):
    monkeypatch.setattr(main_module, "PASSIVE_PORTS_ERROR", None)
    monkeypatch.setattr(main_module, "STREAM_HOST", "127.0.0.1")
    monkeypatch.setattr(main_module, "STREAM_TOKEN", "")
    for key, value in {
        "API_ID": "12345", "API_HASH": "api-hash", "MONGODB": "mongodb://local.test",
        "CHAT_ID": "-100123", "BOT_TOKENS": "token-one",
    }.items():
        monkeypatch.setenv(key, value)
    if missing == "BOT_TOKENS":
        monkeypatch.delenv("BOT_TOKENS", raising=False)
        monkeypatch.delenv("BOT_TOKEN", raising=False)
    else:
        monkeypatch.delenv(missing, raising=False)

    with pytest.raises(RuntimeError, match=expected):
        get_required_config()


def test_get_required_config_rejects_public_stream_without_strong_token(monkeypatch):
    monkeypatch.setattr(main_module, "PASSIVE_PORTS_ERROR", None)
    monkeypatch.setattr(main_module, "STREAM_HOST", "0.0.0.0")
    monkeypatch.setattr(main_module, "STREAM_TOKEN", "short")
    with pytest.raises(RuntimeError, match="at least 32 characters"):
        get_required_config()


@pytest.mark.asyncio
async def test_setup_database_indexes_creates_expected_supplementary_indexes():
    class Files:
        def __init__(self):
            self.calls = []

        async def create_index(self, keys, **kwargs):
            self.calls.append((keys, kwargs))

    files = Files()
    await setup_database_indexes(SimpleNamespace(files=files))
    assert files.calls == [
        ([ ("type", 1), ("parent", 1) ], {"background": True}),
        ([ ("obfuscated_id", 1) ], {"background": True, "sparse": True}),
        ([ ("parts.tg_message", 1) ], {"background": True, "sparse": True}),
        ([ ("queue_identity", 1) ], {"unique": True, "sparse": True, "background": True}),
    ]


@pytest.mark.asyncio
async def test_setup_database_indexes_ignores_existing_index_errors():
    class Files:
        async def create_index(self, _keys, **_kwargs):
            raise OperationFailure("already exists")

    await setup_database_indexes(SimpleNamespace(files=Files()))


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("chat_id", "expected"),
    [("12345", 12345), ("-10012345", -10012345), ("@mulletaflix", "@mulletaflix")],
)
async def test_resolve_channel_normalizes_numeric_ids_and_ignores_startup_ping_failure(
    monkeypatch, chat_id, expected
):
    monkeypatch.setenv("CHAT_ID", chat_id)
    calls = []

    class Bot:
        async def get_chat(self, target):
            calls.append(("get_chat", target))
            return SimpleNamespace(id=expected, title="Nebula")

        async def send_message(self, target, message, **kwargs):
            calls.append(("send_message", target, message, kwargs))
            raise ConnectionError("startup ping unavailable")

    assert await main_module.resolve_channel(Bot()) == expected
    assert calls[0] == ("get_chat", expected)
    assert calls[1][0:3] == ("send_message", expected, "🔄 Nebula FTP MonoBot Conectado")
    assert calls[1][3] == {"disable_notification": True}


@pytest.mark.asyncio
async def test_resolve_channel_returns_none_when_chat_lookup_fails(monkeypatch):
    monkeypatch.setenv("CHAT_ID", "not-a-number")

    class Bot:
        async def get_chat(self, target):
            assert target == "not-a-number"
            raise RuntimeError("chat unavailable")

    assert await main_module.resolve_channel(Bot()) is None
