from __future__ import annotations

import hashlib
import json
import sys
from io import BytesIO
from pathlib import Path
from types import SimpleNamespace

import pytest

from tools import strm_downloader


class _Response:
    def __init__(self, payload: bytes, *, headers=None, status=206):
        self.stream = BytesIO(payload)
        self.headers = headers or {}
        self.status = status

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        self.stream.close()

    def read(self, size=-1):
        return self.stream.read(size)


def _silence_network_globals(monkeypatch):
    monkeypatch.setattr(strm_downloader.socket, "setdefaulttimeout", lambda *_args: None)
    monkeypatch.setattr(strm_downloader, "wait_for_disk_capacity", lambda *_args: None)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: None)


def _write_resume_metadata(tmp_path, url, *, total, parts_count, part_size):
    metadata = {
        "source_fingerprint": hashlib.sha256(url.encode("utf-8")).hexdigest(),
        "total": total,
        "parts_count": parts_count,
        "part_size": part_size,
    }
    (tmp_path / ".episode.download.meta").write_text(json.dumps(metadata), encoding="utf-8")


def _run_args(source, **overrides):
    values = {
        "verbose": False,
        "mongo": "mongodb://test",
        "db_name": "ftp",
        "user": "raphael",
        "dest": str(source / "library"),
        "sources": [str(source)],
        "exclude": None,
        "reset_failures": False,
        "prune_completed": False,
        "dry_run": False,
        "watch": False,
        "parts": 3,
        "min_free_percent": 10,
        "interval": 30,
    }
    values.update(overrides)
    return SimpleNamespace(**values)


def test_cli_parser_applies_environment_defaults_and_all_operational_flags(monkeypatch):
    monkeypatch.setenv("STRM_DOWNLOAD_PARTS", "7")
    monkeypatch.setenv("STRM_MIN_FREE_PERCENT", "23")

    args = strm_downloader.build_parser().parse_args(
        [
            "--sources", "D:/Series", "D:/Movies",
            "--dest", "N:/",
            "--mongo", "mongodb://localhost:27017",
            "--db-name", "nebula",
            "--user", "media-user",
            "--watch", "--prune-completed", "--reset-failures", "--dry-run", "--verbose",
            "--exclude", "tmp,cache",
            "--interval", "45",
        ]
    )

    assert args.sources == ["D:/Series", "D:/Movies"]
    assert args.dest == "N:/"
    assert args.mongo == "mongodb://localhost:27017"
    assert args.db_name == "nebula"
    assert args.user == "media-user"
    assert args.parts == 7
    assert args.min_free_percent == 23
    assert args.watch and args.prune_completed and args.reset_failures and args.dry_run and args.verbose
    assert args.exclude == "tmp,cache"
    assert args.interval == 45


def test_cli_main_returns_downloader_exit_code_and_forwards_parsed_options(monkeypatch):
    monkeypatch.setattr(
        sys,
        "argv",
        ["strm_downloader", "--sources", "D:/Series", "--dest", "N:/", "--parts", "3"],
    )
    received = []

    def run_downloader(args):
        received.append(args)
        return 17

    monkeypatch.setattr(strm_downloader, "run_downloader", run_downloader)

    assert strm_downloader.main() == 17
    assert len(received) == 1
    assert received[0].sources == ["D:/Series"]
    assert received[0].dest == "N:/"
    assert received[0].parts == 3


def test_cli_main_rejects_invalid_numeric_option_before_starting_downloader(monkeypatch):
    monkeypatch.setattr(
        sys,
        "argv",
        ["strm_downloader", "--sources", "D:/Series", "--dest", "N:/", "--parts", "many"],
    )
    monkeypatch.setattr(
        strm_downloader,
        "run_downloader",
        lambda _args: pytest.fail("invalid CLI input must not start the downloader"),
    )

    with pytest.raises(SystemExit) as error:
        strm_downloader.main()

    assert error.value.code == 2


def test_run_downloader_uses_mongo_database_environment_when_cli_flag_is_absent(
    tmp_path, monkeypatch
):
    observed = []
    monkeypatch.setenv("MONGO_DATABASE", "nebula-strm")
    monkeypatch.setenv("MONGODB", "mongodb://mongo.example")
    args = strm_downloader.build_parser().parse_args(
        ["--sources", str(tmp_path / "source"), "--dest", str(tmp_path / "library")]
    )

    class Validator:
        def __init__(self, **kwargs):
            observed.append(kwargs)

        def refresh_cache(self, *, force):
            assert force is True
            return False

    monkeypatch.setattr(strm_downloader, "setup_logger", lambda *_args: None)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])
    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)

    assert strm_downloader.run_downloader(args) == 1
    assert observed == [{
        "mongo_uri": "mongodb://mongo.example",
        "db_name": "nebula-strm",
        "library_user": "raphael",
    }]


def test_run_downloader_prioritizes_partial_downloads_and_skips_recent_failures(tmp_path, monkeypatch):
    source = tmp_path / "source"
    source.mkdir()
    resume = source / "resume.strm"
    fresh = source / "fresh.strm"
    cooldown = source / "cooldown.strm"
    items = [
        (source, fresh, "Filmes", 2024),
        (source, cooldown, "Filmes", 2024),
        (source, resume, "Filmes", 2024),
    ]
    processed = []
    sleeps = []
    tracker_calls = []

    class Validator:
        def __init__(self, **kwargs):
            assert kwargs == {
                "mongo_uri": "mongodb://test",
                "db_name": "ftp",
                "library_user": "raphael",
            }

        def refresh_cache(self, *, force):
            assert force is True
            return True

    class Tracker:
        def reset_cycle(self):
            tracker_calls.append("reset")

        def should_skip(self, path, _staging):
            tracker_calls.append(path.name)
            return path == cooldown, "cooldown active"

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [tmp_path / "stage"])
    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", lambda *_args, **_kwargs: list(items))
    monkeypatch.setattr(
        strm_downloader,
        "has_partial_download_on_disk",
        lambda stem, _staging: stem == "resume",
    )

    def process(**kwargs):
        processed.append(kwargs["strm_path"].name)
        return kwargs["strm_path"] == resume

    monkeypatch.setattr(strm_downloader, "process_strm_item", process)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda seconds: sleeps.append(seconds))

    result = strm_downloader.run_downloader(_run_args(source))

    assert result == 0
    assert processed == ["resume.strm", "fresh.strm"]
    assert tracker_calls == ["reset", "resume.strm", "fresh.strm", "cooldown.strm"]
    assert sleeps == [1]


def test_run_downloader_marks_mongo_ready_before_root_discovery_and_scan(tmp_path, monkeypatch, caplog):
    events = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            assert force is True
            assert "worker-ready" in events
            assert "[STRM-WORKER-MONGO-READY]" not in caplog.text
            assert "[STRM-SCAN-STARTED]" not in caplog.text
            events.append("mongo-refresh-complete")
            return True

    class Tracker:
        def reset_cycle(self):
            pass

        def should_skip(self, *_args):
            return False, ""

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])

    def discover_roots(*_args, **_kwargs):
        assert "mongo-ready" in events
        events.append("root-discovery-started")
        return []

    monkeypatch.setattr(strm_downloader, "get_categorized_scan_batches", discover_roots)

    def scan(*_args, **_kwargs):
        events.append("filesystem-scan-started")
        strm_downloader.logger.info("[STRM-SCAN-STARTED] Varredura iniciada")
        return []

    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", scan)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: None)
    original_info = strm_downloader.logger.info

    def record_info(message, *args, **kwargs):
        if "[STRM-WORKER-READY]" in message:
            events.append("worker-ready")
        elif "[STRM-WORKER-MONGO-READY]" in message:
            events.append("mongo-ready")
        elif "[STRM-SCAN-STARTED]" in message:
            events.append("scan-started")
        original_info(message, *args, **kwargs)

    monkeypatch.setattr(strm_downloader.logger, "info", record_info)
    caplog.set_level("INFO", logger="STRMDownloader")

    assert strm_downloader.run_downloader(_run_args(tmp_path)) == 0
    assert events == ["worker-ready", "mongo-refresh-complete", "mongo-ready", "root-discovery-started", "filesystem-scan-started", "scan-started"]


def test_get_title_scan_roots_reports_inaccessible_directory_and_skips_it(tmp_path, monkeypatch, caplog):
    root = tmp_path / "Series"
    root.mkdir()
    original_iterdir = Path.iterdir

    def fail_for_root(path):
        if path == root:
            raise PermissionError("access denied")
        return original_iterdir(path)

    monkeypatch.setattr(Path, "iterdir", fail_for_root)
    caplog.set_level("WARNING", logger="STRMDownloader")

    result = strm_downloader.get_title_scan_roots([(root, root)])

    assert result == []
    assert "[STRM-SCAN-ROOT-ERROR]" in caplog.text
    assert str(root) in caplog.text


def test_run_downloader_scans_downloads_and_registers_first_strm_after_readiness(
    tmp_path, monkeypatch, caplog
):
    source = tmp_path / "source"
    strm_path = source / "Filmes" / "Alpha (2024)" / "Alpha (2024).strm"
    strm_path.parent.mkdir(parents=True)
    strm_path.write_text("https://media.invalid/alpha.mp4", encoding="utf-8")
    staging_root = tmp_path / "stage"
    events = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            assert force is True
            events.append("mongo-index-ready")
            return True

        def is_already_completed_or_active(self, **_kwargs):
            events.append("duplicate-check")
            return False, "not found"

    class Tracker:
        def reset_cycle(self):
            pass

        def should_skip(self, *_args):
            return False, ""

        def record_success(self, _path):
            events.append("failure-tracker-success")

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [staging_root])
    monkeypatch.setattr(strm_downloader, "remote_content_size", lambda _url: 4)
    monkeypatch.setattr(
        strm_downloader,
        "get_best_staging_root",
        lambda *_args: staging_root,
    )

    def download(*, url, target_path, **_kwargs):
        assert url == "https://media.invalid/alpha.mp4"
        target_path.write_bytes(b"data")
        events.append("download-complete")
        return target_path

    def register(*, downloaded_file, **_kwargs):
        assert downloaded_file.is_file()
        assert downloaded_file.read_bytes() == b"data"
        events.append("queue-registered")
        return True

    monkeypatch.setattr(strm_downloader, "download_strm_multipart", download)
    monkeypatch.setattr(strm_downloader, "register_in_nebula_queue", register)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: None)

    original_info = strm_downloader.logger.info

    def record_info(message, *args, **kwargs):
        if "[STRM-WORKER-READY]" in message:
            events.append("worker-ready")
        elif "[STRM-WORKER-MONGO-READY]" in message:
            events.append("mongo-ready")
        elif "[STRM-SCAN-STARTED]" in message:
            events.append("filesystem-scan-started")
        original_info(message, *args, **kwargs)

    monkeypatch.setattr(strm_downloader.logger, "info", record_info)
    caplog.set_level("INFO", logger="STRMDownloader")

    assert strm_downloader.run_downloader(_run_args(source)) == 0

    assert events.index("worker-ready") < events.index("mongo-index-ready")
    assert events.index("mongo-ready") < events.index("filesystem-scan-started")
    assert events.index("filesystem-scan-started") < events.index("duplicate-check")
    assert events.index("duplicate-check") < events.index("download-complete")
    assert events.index("download-complete") < events.index("queue-registered")
    assert not strm_path.exists()
    assert list(staging_root.rglob("Alpha (2024).mp4"))
    assert "Escaneados=1 | Baixados/Enfileirados=1 | Falhas=0" in caplog.text


def test_run_downloader_fails_closed_when_mongo_index_refresh_fails(tmp_path, monkeypatch, caplog):
    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            assert force is True
            return False

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    caplog.set_level("INFO", logger="STRMDownloader")

    assert strm_downloader.run_downloader(_run_args(tmp_path)) == 1
    assert "[STRM-WORKER-READY]" in caplog.text
    assert "[STRM-WORKER-STARTUP-FAILED]" in caplog.text
    assert "[STRM-SCAN-STARTED]" not in caplog.text


def test_run_downloader_processes_each_category_before_scanning_lower_priority(tmp_path, monkeypatch):
    source = tmp_path / "source"
    animation = source / "Animações" / "A Title.strm"
    film = source / "Filmes" / "B Title.strm"
    animation.parent.mkdir(parents=True)
    film.parent.mkdir(parents=True)
    animation.touch()
    film.touch()
    events = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            assert force is True
            return True

    class Tracker:
        def reset_cycle(self):
            pass

        def should_skip(self, *_args):
            return False, ""

    def scan(_sources, _exclude, **kwargs):
        root, scan_root = kwargs["scan_roots"][0]
        category = strm_downloader.get_category_from_path(scan_root, root)
        media_path = animation if category == "animacoes" else film
        events.append(f"scan:{category}")
        return [(root, media_path, category, 0)]

    def process(**kwargs):
        events.append(f"process:{kwargs['category']}")
        return False

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])
    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", scan)
    monkeypatch.setattr(strm_downloader, "process_strm_item", process)
    monkeypatch.setattr(strm_downloader, "has_partial_download_on_disk", lambda *_args: False)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: None)

    assert strm_downloader.run_downloader(_run_args(source)) == 0
    assert events == [
        "scan:animacoes",
        "process:animacoes",
        "scan:filmes",
        "process:filmes",
    ]


def test_run_downloader_processes_alphabetical_title_before_scanning_next_title(tmp_path, monkeypatch):
    source = tmp_path / "source"
    alpha = source / "Animações" / "Alpha"
    zulu = source / "Animações" / "Zulu"
    alpha_media = alpha / "Alpha.S01E01.strm"
    zulu_media = zulu / "Zulu.S01E01.strm"
    alpha.mkdir(parents=True)
    zulu.mkdir(parents=True)
    alpha_media.touch()
    zulu_media.touch()
    events = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            assert force is True
            return True

    class Tracker:
        def reset_cycle(self):
            pass

        def should_skip(self, *_args):
            return False, ""

    def scan(_sources, _exclude, **kwargs):
        _root, scan_root = kwargs["scan_roots"][0]
        events.append(f"scan:{scan_root.name}")
        media = alpha_media if scan_root == alpha else zulu_media
        return [(source, media, "animacoes", 0)]

    def process(**kwargs):
        events.append(f"process:{kwargs['strm_path'].parent.name}")
        return False

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])
    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", scan)
    monkeypatch.setattr(strm_downloader, "process_strm_item", process)
    monkeypatch.setattr(strm_downloader, "has_partial_download_on_disk", lambda *_args: False)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: None)

    assert strm_downloader.run_downloader(_run_args(source)) == 0
    assert events == [
        "scan:Alpha",
        "process:Alpha",
        "scan:Zulu",
        "process:Zulu",
    ]


def test_run_downloader_prune_only_returns_without_starting_download_scan(tmp_path, monkeypatch):
    observed = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            observed.append(("refresh", force))
            return True

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", lambda: object())
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])
    monkeypatch.setattr(
        strm_downloader,
        "prune_completed_strm_files",
        lambda sources, validator, destination, *, dry_run: observed.append(
            ("prune", sources, destination, dry_run)
        ),
    )
    monkeypatch.setattr(
        strm_downloader,
        "iter_strm_files_prioritized",
        lambda *_args: pytest.fail("prune-only mode must not start the download scan"),
    )

    result = strm_downloader.run_downloader(
        _run_args(tmp_path, prune_completed=True, dry_run=True)
    )

    assert result == 0
    assert observed == [
        ("refresh", True),
        ("prune", [tmp_path.resolve()], (tmp_path / "library").resolve(), True),
    ]


def test_run_downloader_returns_failure_when_scan_raises_in_one_shot_mode(tmp_path, monkeypatch):
    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            return True

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", lambda: object())
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [])

    def fail_scan(*_args):
        raise RuntimeError("scanner unavailable")

    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", fail_scan)
    monkeypatch.setattr(strm_downloader.time, "sleep", lambda *_args: pytest.fail("one-shot mode must return"))

    assert strm_downloader.run_downloader(_run_args(tmp_path)) == 1


def test_multipart_download_keeps_previous_file_if_replacement_fails(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"previous-good-media")
    _silence_network_globals(monkeypatch)

    def fake_urlopen(request, **_kwargs):
        if request.get_header("Range") == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"})
        raise OSError("simulated upstream outage")

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    with pytest.raises(IOError, match="falhou após 1 tentativas"):
        strm_downloader.download_strm_multipart(
            "https://cdn.example/episode.mkv", target, parts_count=1, max_retries=1
        )

    assert target.read_bytes() == b"previous-good-media"


@pytest.mark.parametrize(
    ("content_length", "existing_payload"),
    [("5", b"other"), ("5", b"stale-partial"), (None, b"stale-partial")],
)
def test_multipart_download_replaces_stale_target_when_range_is_unsupported(
    tmp_path, monkeypatch, content_length, existing_payload
):
    target = tmp_path / "episode.mkv"
    target.write_bytes(existing_payload)
    _silence_network_globals(monkeypatch)
    requests = []

    def fake_urlopen(request, **_kwargs):
        requests.append(request.get_header("Range"))
        headers = {"Content-Length": content_length} if content_length is not None else {}
        return _Response(b"fresh", headers=headers, status=200)

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    actual = strm_downloader.download_strm_multipart(
        "https://cdn.example/episode.mkv", target, parts_count=1
    )

    assert actual == target
    assert target.read_bytes() == b"fresh"
    assert requests == ["bytes=0-0"]


def test_multipart_download_replaces_same_size_target_when_range_is_supported(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"stale!")
    _silence_network_globals(monkeypatch)
    requested_ranges = []

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"})
        requested_ranges.append(range_header)
        start, end = map(int, range_header.removeprefix("bytes=").split("-"))
        return _Response(
            b"fresh!"[start : end + 1],
            headers={"Content-Range": f"bytes {start}-{end}/6"},
        )

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    actual = strm_downloader.download_strm_multipart(
        "https://cdn.example/episode.mkv?version=next", target, parts_count=2
    )

    assert actual == target
    assert target.read_bytes() == b"fresh!"
    assert sorted(requested_ranges) == ["bytes=0-2", "bytes=3-5"]


def test_multipart_download_rejects_incomplete_sequential_response_and_preserves_target(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"previous-good-media")
    _silence_network_globals(monkeypatch)

    def fake_urlopen(_request, **_kwargs):
        return _Response(b"bad", headers={"Content-Length": "5"}, status=200)

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    with pytest.raises(IOError, match="Download sequencial incompleto"):
        strm_downloader.download_strm_multipart(
            "https://cdn.example/episode.mkv", target, parts_count=1
        )

    assert target.read_bytes() == b"previous-good-media"


@pytest.mark.parametrize("bad_content_range", ["bytes 3-5/6", "bytes 0-2/7", "invalid range"])
def test_multipart_download_rejects_wrong_content_range_and_preserves_previous_file(
    tmp_path, monkeypatch, bad_content_range
):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"previous-good-media")
    _silence_network_globals(monkeypatch)

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"})
        if range_header == "bytes=0-2":
            return _Response(b"def", headers={"Content-Range": bad_content_range})
        if range_header == "bytes=3-5":
            return _Response(b"def", headers={"Content-Range": "bytes 3-5/6"})
        pytest.fail(f"unexpected range: {range_header}")

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    with pytest.raises(IOError, match="Content-Range"):
        strm_downloader.download_strm_multipart(
            "https://cdn.example/episode.mkv", target, parts_count=2, max_retries=1
        )

    assert target.read_bytes() == b"previous-good-media"


def test_multipart_download_joins_ranges_and_cleans_staging_files(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    payload = b"abcdefghijk"
    _silence_network_globals(monkeypatch)
    ranges = []

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": f"bytes 0-0/{len(payload)}"})
        ranges.append(range_header)
        start, end = map(int, range_header.removeprefix("bytes=").split("-"))
        return _Response(
            payload[start : end + 1],
            headers={"Content-Range": f"bytes {start}-{end}/{len(payload)}"},
        )

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    actual = strm_downloader.download_strm_multipart(
        "https://cdn.example/episode.mkv", target, parts_count=3
    )

    assert actual == target
    assert target.read_bytes() == payload
    assert len(ranges) == 3
    assert list(tmp_path.glob(".episode.*.download*")) == []


def test_multipart_download_resumes_complete_parts_without_redownloading(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    url = "https://cdn.example/episode.mkv"
    _write_resume_metadata(tmp_path, url, total=6, parts_count=2, part_size=3)
    (tmp_path / ".episode.download.part0").write_bytes(b"abc")
    (tmp_path / ".episode.download.part1").write_bytes(b"def")
    _silence_network_globals(monkeypatch)
    requested_ranges = []

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        requested_ranges.append(range_header)
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"})
        pytest.fail(f"complete part unexpectedly downloaded again: {range_header}")

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    actual = strm_downloader.download_strm_multipart(
        url, target, parts_count=2
    )

    assert actual == target
    assert target.read_bytes() == b"abcdef"
    assert requested_ranges == ["bytes=0-0"]


def test_multipart_download_discards_resume_parts_when_source_url_changes(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    old_url = "https://cdn.example/episode.mkv?token=old-secret"
    new_url = "https://cdn.example/episode.mkv?token=new-secret"
    _write_resume_metadata(tmp_path, old_url, total=6, parts_count=2, part_size=3)
    (tmp_path / ".episode.download.part0").write_bytes(b"old")
    (tmp_path / ".episode.download.part1").write_bytes(b"old")
    _silence_network_globals(monkeypatch)
    requested_ranges = []
    payload = b"fresh!"

    def fake_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/6"})
        assert "old-secret" not in (tmp_path / ".episode.download.meta").read_text(encoding="utf-8")
        assert "new-secret" not in (tmp_path / ".episode.download.meta").read_text(encoding="utf-8")
        requested_ranges.append(range_header)
        start, end = map(int, range_header.removeprefix("bytes=").split("-"))
        return _Response(
            payload[start : end + 1],
            headers={"Content-Range": f"bytes {start}-{end}/6"},
        )

    monkeypatch.setattr(strm_downloader, "urlopen", fake_urlopen)

    actual = strm_downloader.download_strm_multipart(new_url, target, parts_count=2)

    assert actual == target
    assert target.read_bytes() == payload
    assert sorted(requested_ranges) == ["bytes=0-2", "bytes=3-5"]


def test_multipart_download_resumes_interrupted_partial_part_and_preserves_good_target(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"previous-good-media")
    _silence_network_globals(monkeypatch)
    first_attempt_ranges = []
    url = "https://cdn.example/episode.mkv"

    def interrupted_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/8"})
        first_attempt_ranges.append(range_header)
        if range_header == "bytes=0-3":
            return _Response(b"ab")  # Transport stops before the requested part is complete.
        return _Response(b"efgh")

    monkeypatch.setattr(strm_downloader, "urlopen", interrupted_urlopen)
    with pytest.raises(IOError, match="Parte 1 falhou após 1 tentativas"):
        strm_downloader.download_strm_multipart(
            url, target, parts_count=2, max_retries=1
        )

    partial = tmp_path / ".episode.download.part0.tmp"
    completed = tmp_path / ".episode.download.part1"
    assert target.read_bytes() == b"previous-good-media"
    assert partial.read_bytes() == b"ab"
    assert completed.read_bytes() == b"efgh"
    assert sorted(first_attempt_ranges) == ["bytes=0-3", "bytes=4-7"]

    retry_ranges = []

    def resumed_urlopen(request, **_kwargs):
        range_header = request.get_header("Range")
        if range_header == "bytes=0-0":
            return _Response(b"", headers={"Content-Range": "bytes 0-0/8"})
        retry_ranges.append(range_header)
        assert range_header == "bytes=2-3"
        return _Response(b"cd")

    monkeypatch.setattr(strm_downloader, "urlopen", resumed_urlopen)
    actual = strm_downloader.download_strm_multipart(
        url, target, parts_count=2, max_retries=1
    )

    assert actual == target
    assert target.read_bytes() == b"abcdefgh"
    assert retry_ranges == ["bytes=2-3"]
    assert list(tmp_path.glob(".episode.*.download*")) == []
