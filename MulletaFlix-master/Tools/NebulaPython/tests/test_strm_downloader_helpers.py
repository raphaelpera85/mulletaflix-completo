from __future__ import annotations

import importlib
import json
import urllib.error
from pathlib import Path
from types import SimpleNamespace

import pytest

downloader = importlib.import_module("tools.strm_downloader")


def test_configured_staging_roots_merge_environment_and_json_and_ignore_invalid_entries(tmp_path, monkeypatch):
    tools_dir = tmp_path / "tools"
    tools_dir.mkdir()
    fake_module = tools_dir / "strm_downloader.py"
    fake_module.touch()
    monkeypatch.setattr(downloader, "__file__", str(fake_module))

    env_root = tmp_path / "env-stage"
    json_root = tmp_path / "json-stage"
    monkeypatch.setenv("STAGING_DIRS", f' "{env_root}" ; {env_root} ')
    monkeypatch.delenv("STAGING_DIR", raising=False)
    (tmp_path / "stage_paths.json").write_text(
        json.dumps([str(env_root), str(json_root), "  ", 42, None]), encoding="utf-8"
    )

    assert downloader.get_configured_staging_dirs() == [env_root.resolve(), json_root.resolve()]


def test_configured_staging_roots_do_not_add_default_when_environment_is_configured(tmp_path, monkeypatch):
    tools_dir = tmp_path / "tools"
    tools_dir.mkdir()
    fake_module = tools_dir / "strm_downloader.py"
    fake_module.touch()
    monkeypatch.setattr(downloader, "__file__", str(fake_module))
    configured = tmp_path / "custom-stage"
    monkeypatch.setenv("STAGING_DIRS", str(configured))
    monkeypatch.delenv("STAGING_DIR", raising=False)

    assert downloader.get_configured_staging_dirs() == [configured.resolve()]


def test_configured_staging_roots_use_default_only_when_no_valid_root_exists(tmp_path, monkeypatch):
    tools_dir = tmp_path / "tools"
    tools_dir.mkdir()
    fake_module = tools_dir / "strm_downloader.py"
    fake_module.touch()
    monkeypatch.setattr(downloader, "__file__", str(fake_module))
    monkeypatch.delenv("STAGING_DIRS", raising=False)
    monkeypatch.delenv("STAGING_DIR", raising=False)
    (tmp_path / "stage_paths.json").write_text('["", 42, null]', encoding="utf-8")

    assert downloader.get_configured_staging_dirs() == [(tmp_path / "staging").resolve()]


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


def test_iter_strm_files_resolves_category_once_per_directory(tmp_path, monkeypatch):
    source = tmp_path / "library"
    directory = source / "Filmes" / "Same Folder"
    media = [directory / f"Movie {index}.mkv" for index in range(3)]
    for path in media:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch()

    calls = []
    original = downloader.get_category_from_path

    def track_category(path, root):
        calls.append((Path(path), Path(root)))
        return original(path, root)

    monkeypatch.setattr(downloader, "get_category_from_path", track_category)
    items = downloader.iter_strm_files_prioritized([source])

    assert [item[1] for item in items] == media
    assert [item[2] for item in items] == ["filmes"] * len(media)
    assert calls == [(directory, source)]


def test_iter_strm_files_reports_scan_progress_without_changing_order(tmp_path, monkeypatch):
    source = tmp_path / "library"
    directory = source / "Filmes"
    paths = [directory / f"Movie {index:02}.mkv" for index in range(5)]
    for path in paths:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch()

    monkeypatch.setattr(downloader, "SCAN_PROGRESS_EVERY_MEDIA", 2)
    progress = []
    items = downloader.iter_strm_files_prioritized(
        [source], progress_callback=lambda directories, media: progress.append((directories, media))
    )

    assert [item[1] for item in items] == paths
    assert progress == [(2, 2), (2, 4), (2, 5)]


def test_categorized_scan_batches_follow_priority_and_keep_original_source_root(tmp_path):
    source = tmp_path / "library"
    animation = source / "Animações"
    films = source / "Filmes"
    series = source / "Series"
    for path in (animation, films, series):
        path.mkdir(parents=True)

    batches = downloader.get_categorized_scan_batches([source])

    assert batches == [
        ("animacoes", [(source, animation)]),
        ("filmes", [(source, films)]),
        ("series", [(source, series)]),
    ]


def test_categorized_scan_batches_keep_unknown_folders_and_loose_media_incremental(tmp_path):
    source = tmp_path / "library"
    films = source / "Filmes"
    unknown = source / "Extras"
    films.mkdir(parents=True)
    unknown.mkdir()
    loose_media = source / "Loose.mkv"
    loose_media.touch()

    assert downloader.get_categorized_scan_batches([source]) == [
        ("filmes", [(source, films)]),
        ("other", [(source, unknown), (source, loose_media)]),
    ]


def test_strm_scan_handles_single_file_scan_root(tmp_path, caplog):
    source = tmp_path / "library"
    category = source / "Extras"
    category.mkdir(parents=True)
    loose_media = category / "Loose.mkv"
    loose_media.touch()
    caplog.set_level("INFO", logger="STRMDownloader")

    items = downloader.iter_strm_files_prioritized(
        [source], scan_roots=[(source, loose_media)]
    )

    assert [(item[1], item[2]) for item in items] == [(loose_media, "other")]
    assert "[STRM-SCAN-STARTED]" in caplog.text


def test_title_scan_roots_split_category_into_alphabetical_titles(tmp_path):
    source = tmp_path / "library"
    category = source / "Animações"
    roots = [category / title for title in ("Zulu", "Alpha", "Beta")]
    for root in roots:
        root.mkdir(parents=True)

    assert downloader.get_title_scan_roots([(source, category)]) == [
        (source, category / "Alpha"),
        (source, category / "Beta"),
        (source, category / "Zulu"),
    ]


def test_title_scan_roots_split_direct_media_and_skip_excluded_titles(tmp_path):
    source = tmp_path / "library"
    category = source / "Filmes"
    (category / "Zulu").mkdir(parents=True)
    (category / "Temp").mkdir()
    direct_media = category / "Loose.mkv"
    direct_media.touch()

    assert downloader.get_title_scan_roots([(source, category)]) == [
        (source, direct_media),
        (source, category / "Temp"),
        (source, category / "Zulu"),
    ]

    direct_media.unlink()
    assert downloader.get_title_scan_roots(
        [(source, category)], exclude_dirs={"Temp"}
    ) == [(source, category / "Zulu")]


def _create_directory_symlink(link: Path, target: Path) -> None:
    try:
        link.symlink_to(target, target_is_directory=True)
    except (NotImplementedError, OSError) as exc:
        pytest.skip(f"O host não permite criar symlink de diretório: {exc}")


def test_categorized_scan_batches_do_not_promote_linked_category_roots(tmp_path):
    source = tmp_path / "library"
    external = tmp_path / "outside" / "Animações"
    source.mkdir()
    external.mkdir(parents=True)
    _create_directory_symlink(source / "Animações", external)

    assert downloader.get_categorized_scan_batches([source]) is None


@pytest.mark.parametrize("target_kind", ["outside", "ancestor"])
def test_title_scan_roots_skip_symlinks_to_outside_or_ancestor(tmp_path, target_kind):
    source = tmp_path / "library"
    category = source / "Animações"
    normal_title = category / "Alpha"
    normal_title.mkdir(parents=True)
    outside = tmp_path / "outside"
    outside.mkdir()
    link_target = outside if target_kind == "outside" else source
    _create_directory_symlink(category / "Linked", link_target)

    assert downloader.get_title_scan_roots([(source, category)]) == [(source, normal_title)]


def test_strm_scan_rejects_symlink_scan_root_outside_monitor_path(tmp_path):
    source = tmp_path / "library"
    external = tmp_path / "outside"
    source.mkdir()
    external.mkdir()
    (external / "outside.strm").write_text("https://example.invalid/media", encoding="utf-8")
    linked_root = source / "linked"
    _create_directory_symlink(linked_root, external)

    found = downloader.iter_strm_files_prioritized(
        [source], scan_roots=[(source, linked_root)]
    )

    assert found == []


def test_strm_scan_logs_readiness_when_filesystem_walk_begins(tmp_path, caplog):
    source = tmp_path / "library"
    source.mkdir()
    caplog.set_level("INFO", logger="STRMDownloader")

    assert downloader.iter_strm_files_prioritized(
        [source], scan_roots=[(source, source)]
    ) == []
    assert "[STRM-SCAN-STARTED]" in caplog.text


def test_strm_scan_does_not_report_start_when_root_cannot_be_enumerated(tmp_path, caplog, monkeypatch):
    source = tmp_path / "library"
    source.mkdir()
    caplog.set_level("INFO", logger="STRMDownloader")

    def inaccessible_walk(_root, onerror=None):
        if onerror is not None:
            onerror(PermissionError("access denied"))
        return iter(())

    monkeypatch.setattr(downloader.os, "walk", inaccessible_walk)

    assert downloader.iter_strm_files_prioritized(
        [source], scan_roots=[(source, source)]
    ) == []
    assert "[STRM-SCAN-ERROR]" in caplog.text
    assert "[STRM-SCAN-STARTED]" not in caplog.text


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


def test_wait_for_disk_capacity_retries_if_disk_probe_fails(tmp_path, monkeypatch, caplog):
    target = tmp_path / "stage" / "media.mkv"
    gib = 1024**3
    checks = []
    sleeps = []

    def disk_usage(path):
        checks.append(Path(path))
        if len(checks) == 1:
            raise PermissionError("disk unavailable")
        return SimpleNamespace(total=100 * gib, free=20 * gib)

    monkeypatch.setattr(downloader.shutil, "disk_usage", disk_usage)
    monkeypatch.setattr(downloader.time, "time", lambda: 31)
    monkeypatch.setattr(downloader.time, "sleep", sleeps.append)

    downloader.wait_for_disk_capacity(target, required_bytes=100, poll_seconds=3)

    assert checks == [target.parent, target.parent]
    assert sleeps == [3]
    assert "download pausado até o volume ficar disponível" in caplog.text


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
    assert validator.refresh_cache(force=True) is True

    assert clients[0].closed is True
    assert validator.completed_movies == {("arrival", "2016")}
    assert validator.completed_episodes == {("atomic", 1, 2)}
    assert ("/raphael/novelas/work", "queued work.s01e01.mkv") in validator.active_exact
    assert "d:\\stage\\queued-work.mkv" in validator.active_paths

    duplicate_movie = tmp_path / "Arrival (2016).strm"
    assert validator.is_already_completed_or_active(duplicate_movie)[0] is True
    duplicate_episode = tmp_path / "Atomic" / "Atomic.S01E02.strm"
    assert validator.is_already_completed_or_active(duplicate_episode)[0] is True
    active_item = tmp_path / "Novelas" / "Work" / "Queued Work.S01E01.strm"
    active_destination = tmp_path / "Novelas" / "Work" / "Queued Work.S01E01.mkv"
    assert validator.is_already_completed_or_active(
        active_item, destination=active_destination, dest_root=tmp_path
    )[0] is True


def test_media_validator_does_not_delete_same_episode_stem_from_another_series(tmp_path):
    validator = downloader.MediaValidator("mongodb://test")
    validator.active_episodes = {("show a", 1, 1)}

    same_episode_number = tmp_path / "Show B" / "Season 01" / "Show B.S01E01.strm"
    duplicate, _ = validator.is_already_completed_or_active(same_episode_number)

    assert duplicate is False


def test_media_validator_detects_same_active_episode_identity_in_same_series(tmp_path):
    validator = downloader.MediaValidator("mongodb://test")
    validator.active_episodes = {("show a", 1, 1)}

    same_episode = tmp_path / "Show A" / "Season 01" / "Show A - S01E01.strm"

    assert validator.is_already_completed_or_active(same_episode)[0] is True


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
    assert validator.refresh_cache(force=True) is True
    assert validator.refresh_cache() is True

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


def test_media_validator_throttles_retry_after_mongo_index_failure(monkeypatch):
    calls = []

    class Files:
        def find(self, *_args, **_kwargs):
            def fail_during_iteration():
                yield {"parent": "/raphael/Filmes", "name": "partial.mkv", "status": "completed"}
                raise RuntimeError("simulated Mongo cursor interruption")

            return fail_during_iteration()

    class Database:
        files = Files()

    class Client:
        def __getitem__(self, _name):
            return Database()

        def close(self):
            pass

    def create_client(*_args, **_kwargs):
        calls.append("connect")
        return Client()

    monkeypatch.setattr(downloader, "MongoClient", create_client)
    monkeypatch.setattr(downloader.time, "time", lambda: 50_000.0)
    validator = downloader.MediaValidator("mongodb://mongo.example")

    assert validator.refresh_cache(force=True) is False
    assert validator.refresh_cache() is False

    assert calls == ["connect"]
    assert validator.last_cache_time == 0.0
    assert validator.last_cache_attempt_time == 50_000.0


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
