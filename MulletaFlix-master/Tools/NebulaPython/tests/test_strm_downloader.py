from __future__ import annotations

from pathlib import Path
from io import BytesIO
from types import SimpleNamespace
import sys

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


def test_run_downloader_prioritizes_partial_downloads_and_skips_recent_failures(tmp_path, monkeypatch):
    source = tmp_path / "source"
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

    class Tracker:
        def reset_cycle(self):
            tracker_calls.append("reset")

        def should_skip(self, path, _staging):
            tracker_calls.append(path.name)
            return path == cooldown, "cooldown active"

    monkeypatch.setattr(strm_downloader, "MediaValidator", Validator)
    monkeypatch.setattr(strm_downloader, "FailureTracker", Tracker)
    monkeypatch.setattr(strm_downloader, "get_configured_staging_dirs", lambda: [tmp_path / "stage"])
    monkeypatch.setattr(strm_downloader, "iter_strm_files_prioritized", lambda *_args: list(items))
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


def test_run_downloader_prune_only_returns_without_starting_download_scan(tmp_path, monkeypatch):
    observed = []

    class Validator:
        def __init__(self, **_kwargs):
            pass

        def refresh_cache(self, *, force):
            observed.append(("refresh", force))

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
            pass

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
        "https://cdn.example/episode.mkv", target, parts_count=2
    )

    assert actual == target
    assert target.read_bytes() == b"abcdef"
    assert requested_ranges == ["bytes=0-0"]


def test_multipart_download_resumes_interrupted_partial_part_and_preserves_good_target(tmp_path, monkeypatch):
    target = tmp_path / "episode.mkv"
    target.write_bytes(b"previous-good-media")
    _silence_network_globals(monkeypatch)
    first_attempt_ranges = []

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
            "https://cdn.example/episode.mkv", target, parts_count=2, max_retries=1
        )

    partial = tmp_path / ".episode.download.part0.tmp"
    completed = tmp_path / ".episode.download.part1"
    assert target.read_bytes() == b"previous-good-media"
    assert partial.read_bytes() == b"ab"
    assert completed.read_bytes() == b"efgh"
    assert first_attempt_ranges == ["bytes=0-3", "bytes=4-7"]

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
        "https://cdn.example/episode.mkv", target, parts_count=2, max_retries=1
    )

    assert actual == target
    assert target.read_bytes() == b"abcdefgh"
    assert retry_ranges == ["bytes=2-3"]
    assert list(tmp_path.glob(".episode.*.download*")) == []
