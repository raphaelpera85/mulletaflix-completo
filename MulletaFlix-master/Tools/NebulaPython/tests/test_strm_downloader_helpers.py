from __future__ import annotations

import importlib
import urllib.error
from pathlib import Path
from types import SimpleNamespace

import pytest

downloader = importlib.import_module("tools.strm_downloader")


@pytest.mark.parametrize(
    ("name", "expected"),
    [
        ("movie.mkv", False), ("movie.mkv.partial", True), ("movie.crdownload", True),
        ("movie.part", True), ("movie.part01", True), ("movie.download.part2", True),
        ("movie.tmp", True), ("movie.PARTIAL", True),
    ],
)
def test_incomplete_filename_detection(name, expected):
    assert downloader.is_incomplete_filename(name) is expected


@pytest.mark.parametrize(
    ("name", "parent", "expected"),
    [("Movie (2001).mkv", "", 2001), ("Movie.2020.1080p", "", 2020), ("episode.mkv", "Series (1998)", 1998), ("Movie (1899)", "", 0)],
)
def test_extract_media_year_from_filename_or_parent(name, parent, expected):
    assert downloader.extract_media_year(name, parent) == expected


def test_movie_identity_normalizes_accents_punctuation_and_year():
    assert downloader.movie_identity("Amélie - Le Fabuleux Destin (2001).mkv") == (
        "amelie le fabuleux destin", "2001"
    )
    assert downloader.movie_identity("No release year.mkv") is None


@pytest.mark.parametrize(
    ("series", "filename", "expected"),
    [
        ("Breaking Bad", "Breaking.Bad.S02E03.mkv", ("breaking bad", 2, 3)),
        ("Season 1", "Series_Name_1x09.mp4", ("series name", 1, 9)),
        ("", "Show.S01E02.mkv", ("show", 1, 2)),
        ("", "movie.mkv", None),
    ],
)
def test_episode_identity_uses_series_name_or_filename_prefix(series, filename, expected):
    assert downloader.episode_identity(series, filename) == expected


@pytest.mark.parametrize(
    ("relative", "expected"),
    [
        ("Animações/Foo.mkv", "animacoes"), ("Movies/Foo.mkv", "filmes"),
        ("K-Drama/Foo.mkv", "doramas"), ("Novelas/Foo.mkv", "novelas"),
        ("XXX/Foo.mkv", "porno"), ("TV/Foo.mkv", "series"), ("Misc/Foo.mkv", "other"),
    ],
)
def test_category_detection_from_source_hierarchy(tmp_path, relative, expected):
    root = tmp_path / "source"
    assert downloader.get_category_from_path(root / relative, root) == expected


def test_read_strm_url_ignores_bom_blank_lines_and_comments(tmp_path):
    source = tmp_path / "title.strm"
    source.write_text("\ufeff\n# metadata\n  https://media.example/title.mkv?token=x  \n", encoding="utf-8")
    assert downloader.read_strm_url(source) == "https://media.example/title.mkv?token=x"


@pytest.mark.parametrize("content", ["", "# only a comment\n\n"])
def test_read_strm_url_rejects_empty_or_comment_only_file(tmp_path, content):
    source = tmp_path / "empty.strm"
    source.write_text(content, encoding="utf-8")
    with pytest.raises(ValueError, match="vazio ou inválido"):
        downloader.read_strm_url(source)


@pytest.mark.parametrize(
    ("url", "content_type", "expected"),
    [
        ("https://cdn.example/video.mkv?sig=1", None, ".mkv"),
        ("https://cdn.example/no-extension", "video/mp4; charset=binary", ".mp4"),
        ("https://cdn.example/no-extension", "image/jpeg", ".mp4"),
        ("https://cdn.example/no-extension", "application/octet-stream", ".mp4"),
    ],
)
def test_guess_media_extension_prefers_supported_url_then_content_type(url, content_type, expected):
    assert downloader.guess_media_extension(url, content_type) == expected


class _Response:
    def __init__(self, headers, status=206):
        self.headers = headers
        self.status = status

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return None


@pytest.mark.parametrize(
    ("headers", "status", "expected"),
    [
        ({"Content-Range": "bytes 0-0/9876"}, 206, 9876),
        ({"Content-Length": "123"}, 200, 123),
        ({"Content-Length": "123"}, 206, None),
        ({}, 200, None),
    ],
)
def test_remote_content_size_parses_range_and_length(monkeypatch, headers, status, expected):
    monkeypatch.setattr(downloader, "urlopen", lambda *_args, **_kwargs: _Response(headers, status))
    assert downloader.remote_content_size("https://cdn.example/media") == expected


def test_remote_content_size_treats_network_errors_as_unknown(monkeypatch):
    monkeypatch.setattr(downloader, "urlopen", lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("offline")))
    assert downloader.remote_content_size("https://cdn.example/media") is None


@pytest.mark.parametrize(
    ("relative", "expected"),
    [
        ("Filmes/Arrival (2016).mkv", Path("Filmes/Arrival (2016)/Arrival (2016).mkv")),
        ("Series/Show/Season 01/Episode.mkv", Path("Series/Show/Season 01/Episode.mkv")),
        ("Doramas/Drama.strm", Path("Doramas/Drama.strm")),
    ],
)
def test_destination_for_preserves_library_shape_or_groups_movies(tmp_path, relative, expected):
    source_root, destination_root = tmp_path / "src", tmp_path / "dst"
    src = source_root / relative
    assert downloader.destination_for(source_root, destination_root, src) == destination_root / expected


def test_series_destination_uses_filename_episode_pattern(tmp_path):
    dest = downloader.destination_for(tmp_path / "source", tmp_path / "dest", tmp_path / "Show.S03E04.mkv")
    assert dest == tmp_path / "dest" / "Series" / "Show" / "Season 03" / "Show.S03E04.mkv"
    assert downloader.series_path_from_filename(tmp_path / "dest", tmp_path / "feature.mkv") is None


def test_mongo_parent_uses_destination_relative_path_and_safe_fallback(tmp_path):
    root = tmp_path / "library"
    assert downloader.mongo_parent_for(root, root / "Doramas" / "Drama") == "/raphael/Doramas/Drama"
    assert downloader.mongo_parent_for(root, tmp_path / "outside") == "/raphael"


def test_best_staging_root_respects_priority_reserve_then_free_space(tmp_path, monkeypatch):
    first, second = tmp_path / "fast", tmp_path / "large"
    first.mkdir()
    second.mkdir()
    gib = 1024**3
    monkeypatch.setattr(downloader.shutil, "disk_usage", lambda p: {
        first: SimpleNamespace(total=100 * gib, free=15 * gib),
        second: SimpleNamespace(total=100 * gib, free=80 * gib),
    }[Path(p)])
    assert downloader.get_best_staging_root([first, second], required_bytes=gib, min_free_percent=10) == first


def test_best_staging_root_falls_back_to_disk_with_most_free_bytes(tmp_path, monkeypatch):
    first, second = tmp_path / "first", tmp_path / "second"
    first.mkdir()
    second.mkdir()
    gib = 1024**3
    free = {first: 8 * gib, second: 9 * gib}
    monkeypatch.setattr(downloader.shutil, "disk_usage", lambda p: SimpleNamespace(total=100 * gib, free=free[Path(p)]))

    assert downloader.get_best_staging_root([first, second], required_bytes=gib, min_free_percent=10) == second


def test_best_staging_root_enforces_five_gib_floor_and_clamps_reserve(tmp_path, monkeypatch):
    stage = tmp_path / "stage"
    stage.mkdir()
    gib = 1024**3
    monkeypatch.setattr(downloader.shutil, "disk_usage", lambda _path: SimpleNamespace(total=100 * gib, free=6 * gib))

    assert downloader.get_best_staging_root([stage], required_bytes=gib, min_free_percent=0) == stage


def test_iter_strm_files_skips_missing_sources_excluded_roots_and_non_media(tmp_path):
    source = tmp_path / "library"
    movie = source / "Filmes" / "Arrival (2016).mkv"
    loose_video = source / "misc" / "extra.mp4"
    excluded_video = source / "temp" / "stale.mkv"
    incomplete = source / "Filmes" / "unfinished.mkv.partial"
    sidecar = source / "Filmes" / "poster.jpg"
    for path in (movie, loose_video, excluded_video, incomplete, sidecar):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch()

    items = downloader.iter_strm_files_prioritized(
        [source, tmp_path / "missing-library"], exclude_dirs={"TEMP"}
    )

    assert [(path, category, year) for _root, path, category, year in items] == [
        (movie, "filmes", 2016),
        (loose_video, "other", 0),
    ]


def test_wait_for_disk_capacity_sleeps_and_rechecks_until_capacity_is_available(tmp_path, monkeypatch):
    target = tmp_path / "stage" / "media.mkv"
    gib = 1024**3
    usages = iter([
        SimpleNamespace(total=100 * gib, free=5 * gib),
        SimpleNamespace(total=100 * gib, free=20 * gib),
    ])
    checks = []
    sleeps = []
    monkeypatch.setattr(downloader.shutil, "disk_usage", lambda path: checks.append(Path(path)) or next(usages))
    monkeypatch.setattr(downloader.time, "time", lambda: 31)
    monkeypatch.setattr(downloader.time, "sleep", sleeps.append)

    downloader.wait_for_disk_capacity(target, required_bytes=2 * gib, min_free_percent=10, poll_seconds=3)

    assert target.parent.is_dir()
    assert checks == [target.parent, target.parent]
    assert sleeps == [3]


def test_wait_for_disk_capacity_returns_if_disk_probe_fails(tmp_path, monkeypatch):
    monkeypatch.setattr(
        downloader.shutil,
        "disk_usage",
        lambda _path: (_ for _ in ()).throw(PermissionError("disk unavailable")),
    )

    assert downloader.wait_for_disk_capacity(tmp_path / "stage" / "media.mkv", required_bytes=100) is None


def test_existing_staging_target_detects_completed_file_and_partial(tmp_path):
    dest_root, stage_root = tmp_path / "library", tmp_path / "stage"
    destination = dest_root / "Filmes" / "Film" / "film.mkv"
    staged = stage_root / "Filmes" / "Film"
    staged.mkdir(parents=True)
    part = staged / ".film.download.part0"
    part.write_bytes(b"partial")
    target, exists = downloader.find_existing_staging_target(dest_root, destination, [stage_root])
    assert target == staged / "film.mkv"
    assert exists


def test_series_key_and_display_name_use_parent_of_season_folder(tmp_path):
    episode = tmp_path / "The Show" / "Season 02" / "Episode S02E03.mkv"
    assert downloader.get_series_key(episode) == "the show"
    assert downloader.get_series_display_name(episode) == "The Show"


def test_failure_tracker_persists_failures_and_skips_only_after_transient_threshold(tmp_path, monkeypatch):
    now = [10_000.0]
    monkeypatch.setattr(downloader.time, "time", lambda: now[0])
    cache = tmp_path / "failures.json"
    media = tmp_path / "Show" / "Season 01" / "Show.S01E01.mkv"
    tracker = downloader.FailureTracker(cache, cooldown_seconds=60)

    for _ in range(2):
        tracker.record_failure(media, "connection reset")
    assert tracker.should_skip(media) == (False, "")
    tracker.record_failure(media, "connection reset")

    restored = downloader.FailureTracker(cache, cooldown_seconds=60)
    should_skip, reason = restored.should_skip(media)
    assert should_skip is True
    assert "connection reset" in reason
    assert "Cooldown ativo" in reason

    now[0] += 61
    assert restored.should_skip(media) == (False, "")


def test_failure_tracker_fatal_series_breaker_resets_and_partial_download_overrides_skip(
    tmp_path, monkeypatch
):
    monkeypatch.setattr(downloader.time, "time", lambda: 20_000.0)
    cache = tmp_path / "failures.json"
    tracker = downloader.FailureTracker(cache, cooldown_seconds=60)
    series = tmp_path / "The Show" / "Season 01"
    first = series / "The.Show.S01E01.mkv"
    second = series / "The.Show.S01E02.mkv"
    third = series / "The.Show.S01E03.mkv"

    for episode in (first, second, third):
        tracker.record_failure(episode, "HTTP Error 404: Not Found")
    assert tracker.is_series_broken(first)[0] is True
    assert tracker.should_skip(second)[0] is True

    staging = tmp_path / "staging"
    staging.mkdir()
    (staging / ".The.Show.S01E02.download.part0").write_bytes(b"partial")
    assert tracker.should_skip(second, [staging]) == (False, "")

    tracker.reset_cycle()
    assert tracker.is_series_broken(first)[0] is False
    restored = downloader.FailureTracker(cache, cooldown_seconds=60)
    assert restored.should_skip(first)[0] is True  # persisted fatal cooldown survives process recreation


def test_failure_tracker_success_clears_persisted_failure(tmp_path, monkeypatch):
    monkeypatch.setattr(downloader.time, "time", lambda: 30_000.0)
    cache = tmp_path / "failures.json"
    media = tmp_path / "film.mkv"
    tracker = downloader.FailureTracker(cache, cooldown_seconds=60)
    for _ in range(3):
        tracker.record_failure(media, "connection reset")

    tracker.record_success(media)

    assert downloader.FailureTracker(cache, cooldown_seconds=60).should_skip(media) == (False, "")


def test_media_validator_indexes_completed_movies_episodes_and_active_items(tmp_path, monkeypatch):
    documents = [
        {
            "parent": "/raphael/Filmes/Arrival (2016)",
            "name": "Arrival (2016).mkv",
            "status": "completed",
        },
        {
            "parent": "/raphael/Series/Atomic/Season 01",
            "name": "Atomic.S01E02.mkv",
            "status": "completed",
        },
        {
            "parent": "/raphael/Novelas/Work",
            "name": "Queued Work.S01E01.mkv",
            "status": "uploading",
            "local_path": "D:/stage/queued-work.mkv",
        },
        {"parent": "/raphael/Filmes/ignored", "name": "", "status": "completed"},
    ]

    class Files:
        def find(self, query, projection):
            assert query == {"type": "file"}
            assert projection == {"parent": 1, "name": 1, "status": 1, "local_path": 1}
            return iter(documents)

    class Database:
        files = Files()

    class Client:
        closed = False

        def __getitem__(self, name):
            assert name == "ftp"
            return Database()

        def close(self):
            self.closed = True

    clients = []

    def create_client(*_args, **_kwargs):
        client = Client()
        clients.append(client)
        return client

    monkeypatch.setattr(downloader, "MongoClient", create_client)
    validator = downloader.MediaValidator("mongodb://mongo.example")
    validator.refresh_cache(force=True)

    assert clients[0].closed is True
    assert validator.completed_movies == {("arrival", "2016")}
    assert validator.completed_episodes == {("atomic", 1, 2)}
    assert "queued work.s01e01.mkv" in validator.active_paths_or_names
    assert "d:/stage/queued-work.mkv" in validator.active_paths_or_names

    duplicate_movie = tmp_path / "Arrival (2016).strm"
    assert validator.is_already_completed_or_active(duplicate_movie)[0] is True
    duplicate_episode = tmp_path / "Atomic" / "Atomic.S01E02.strm"
    assert validator.is_already_completed_or_active(duplicate_episode)[0] is True
    active_item = tmp_path / "Queued Work.S01E01.strm"
    assert validator.is_already_completed_or_active(active_item)[0] is True


def test_media_validator_matches_exact_destination_and_reuses_cache(tmp_path, monkeypatch):
    documents = [
        {
            "parent": "/raphael/Doramas/Drama",
            "name": "Episode.S01E01.mkv",
            "status": "completed",
        }
    ]

    class Files:
        def find(self, *_args, **_kwargs):
            return iter(documents)

    class Database:
        files = Files()

    class Client:
        def __getitem__(self, _name):
            return Database()

        def close(self):
            pass

    clients = []

    def create_client(*_args, **_kwargs):
        clients.append(object())
        return Client()

    monkeypatch.setattr(downloader, "MongoClient", create_client)
    monkeypatch.setattr(downloader.time, "time", lambda: 50_000.0)
    validator = downloader.MediaValidator("mongodb://mongo.example")
    validator.refresh_cache(force=True)
    validator.refresh_cache()

    assert len(clients) == 1
    destination_root = tmp_path / "library"
    destination = destination_root / "Doramas" / "Drama" / "Episode.S01E01.mp4"
    duplicate, reason = validator.is_already_completed_or_active(
        tmp_path / "incoming.strm",
        destination=destination,
        dest_root=destination_root,
    )
    assert duplicate is True
    assert "já concluído no Nebula" in reason


@pytest.mark.parametrize(
    ("code", "headers", "expected"),
    [
        (403, {}, "HTTP Error 403: Forbidden"),
        (401, {}, "HTTP Error 401: Unauthorized"),
        (404, {}, "HTTP Error 404: Not Found"),
        (406, {"X-Debug-ASNAllowed": "0"}, "Bloqueio de Operadora/ASN"),
    ],
)
def test_download_error_formatter_explains_common_http_failures(code, headers, expected):
    error = urllib.error.HTTPError("https://cdn.example/a", code, "rejected", headers, None)
    assert expected in downloader.format_download_error(error)
