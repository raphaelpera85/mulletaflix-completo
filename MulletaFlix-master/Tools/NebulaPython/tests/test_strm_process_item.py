from __future__ import annotations

import importlib
from pathlib import Path

import pytest

downloader = importlib.import_module("tools.strm_downloader")


class Validator:
    def __init__(self, duplicate=False):
        self.duplicate = duplicate
        self.calls = []

    def is_already_completed_or_active(self, **kwargs):
        self.calls.append(kwargs)
        return self.duplicate, "already indexed" if self.duplicate else "new"


class FailureTracker:
    def __init__(self):
        self.failures = []
        self.successes = []

    def record_failure(self, path, reason):
        self.failures.append((path, reason))

    def record_success(self, path):
        self.successes.append(path)


def _configure_common(monkeypatch, *, duplicate=False):
    validator = Validator(duplicate)
    tracker = FailureTracker()
    deletions = []
    monkeypatch.setattr(downloader, "delete_strm_and_empty_parents", lambda *args, **kwargs: deletions.append((args, kwargs)))
    monkeypatch.setattr(downloader, "get_best_staging_root", lambda roots, *_args: roots[0])
    monkeypatch.setattr(downloader, "find_existing_staging_target", lambda **_kwargs: (None, False))
    return validator, tracker, deletions


def _process(source_root, strm_path, dest_root, staging_root, validator, tracker, **kwargs):
    return downloader.process_strm_item(
        source_root=source_root,
        strm_path=strm_path,
        category="series",
        year=2026,
        dest_root=dest_root,
        staging_dirs=[staging_root],
        validator=validator,
        mongo_uri="mongodb://fake",
        db_name="nebula-test",
        library_user="raphael",
        parts_count=2,
        failure_tracker=tracker,
        **kwargs,
    )


def test_process_strm_item_records_read_error_and_keeps_source(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "broken.strm"
    source.write_text("# no URL", encoding="utf-8")
    validator, tracker, deletions = _configure_common(monkeypatch)

    assert not _process(source_root, source, tmp_path / "dest", tmp_path / "stage", validator, tracker)
    assert source.exists()
    assert tracker.failures and "Erro de leitura" in tracker.failures[0][1]
    assert not deletions


def test_process_strm_item_duplicate_removes_source_and_marks_success(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "duplicate.strm"
    source.write_text("https://media.example/a.mkv", encoding="utf-8")
    validator, tracker, deletions = _configure_common(monkeypatch, duplicate=True)

    result = _process(source_root, source, tmp_path / "dest", tmp_path / "stage", validator, tracker)

    assert result is False
    assert deletions[0][0] == (source, source_root)
    assert tracker.successes == [source]


def test_process_strm_item_dry_run_never_downloads_or_registers(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "preview.strm"
    source.write_text("https://media.example/preview.mkv", encoding="utf-8")
    validator, tracker, deletions = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "remote_content_size", lambda _url: 100)
    monkeypatch.setattr(downloader, "download_strm_multipart", lambda **_kwargs: pytest.fail("dry-run downloaded media"))
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **_kwargs: pytest.fail("dry-run registered media"))

    assert _process(source_root, source, tmp_path / "dest", tmp_path / "stage", validator, tracker, dry_run=True)
    assert source.exists()
    assert deletions[0][1] == {"dry_run": True}
    assert not tracker.failures and not tracker.successes


def test_process_strm_item_download_error_preserves_source_and_tracks_failure(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "failed.strm"
    source.write_text("https://media.example/failed.mkv", encoding="utf-8")
    validator, tracker, deletions = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "remote_content_size", lambda _url: 100)
    monkeypatch.setattr(downloader, "download_strm_multipart", lambda **_kwargs: (_ for _ in ()).throw(OSError("offline")))

    assert not _process(source_root, source, tmp_path / "dest", tmp_path / "stage", validator, tracker)
    assert source.exists()
    assert tracker.failures[0][0] == source
    assert "offline" in tracker.failures[0][1]
    assert not deletions


def test_process_strm_item_success_registers_then_removes_strm(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "episode.strm"
    source.write_text("https://media.example/episode.mkv", encoding="utf-8")
    dest_root, stage_root = tmp_path / "dest", tmp_path / "stage"
    validator, tracker, deletions = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "remote_content_size", lambda _url: 100)
    downloaded = stage_root / "episode.mkv"
    monkeypatch.setattr(downloader, "download_strm_multipart", lambda **kwargs: kwargs["target_path"])
    registrations = []
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **kwargs: registrations.append(kwargs) or True)

    assert _process(source_root, source, dest_root, stage_root, validator, tracker)
    assert registrations[0]["downloaded_file"] == downloaded
    assert registrations[0]["delete_source"] is True
    assert deletions[0][0] == (source, source_root)
    assert tracker.successes == [source]
    assert not tracker.failures


def test_process_strm_item_keeps_source_when_queue_registration_fails(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "not-queued.strm"
    source.write_text("https://media.example/not-queued.mkv", encoding="utf-8")
    validator, tracker, deletions = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "remote_content_size", lambda _url: 100)
    monkeypatch.setattr(downloader, "download_strm_multipart", lambda **kwargs: kwargs["target_path"])
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **_kwargs: False)

    assert not _process(source_root, source, tmp_path / "dest", tmp_path / "stage", validator, tracker)
    assert source.exists()
    assert not deletions and not tracker.successes


def test_process_local_media_moves_to_stage_before_queue_registration(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "movie.mp4"
    source.write_bytes(b"real local media fixture")
    dest_root, stage_root = tmp_path / "dest", tmp_path / "stage"
    validator, tracker, deletions = _configure_common(monkeypatch)
    waits = []
    monkeypatch.setattr(downloader, "wait_for_disk_capacity", lambda *args: waits.append(args))
    registrations = []
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **kwargs: registrations.append(kwargs) or True)

    assert _process(source_root, source, dest_root, stage_root, validator, tracker)
    target = stage_root / "movie.mp4"
    assert target.read_bytes() == b"real local media fixture"
    assert not source.exists()
    assert waits and registrations[0]["downloaded_file"] == target
    assert tracker.successes == [source]
    assert deletions[0][0] == (source, source_root)


def test_process_local_media_restores_source_when_queue_registration_fails(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "movie.mp4"
    payload = b"only copy of local media"
    source.write_bytes(payload)
    dest_root, stage_root = tmp_path / "dest", tmp_path / "stage"
    validator, tracker, deletions = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "wait_for_disk_capacity", lambda *_args: None)
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **_kwargs: False)

    result = _process(source_root, source, dest_root, stage_root, validator, tracker)

    assert result is False
    assert source.read_bytes() == payload
    assert not (stage_root / "movie.mp4").exists()
    assert tracker.failures == [(source, "Falha ao registrar mídia na fila Mongo")]
    assert tracker.successes == []
    assert not deletions


def test_process_local_media_does_not_overwrite_source_that_reappears_before_rollback(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "movie.mp4"
    source.write_bytes(b"original")
    stage_root = tmp_path / "stage"
    validator, tracker, _ = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "wait_for_disk_capacity", lambda *_args: None)

    def reject_registration(**_kwargs):
        source.write_bytes(b"new source")
        return False

    monkeypatch.setattr(downloader, "register_in_nebula_queue", reject_registration)

    assert not _process(source_root, source, tmp_path / "dest", stage_root, validator, tracker)

    assert source.read_bytes() == b"new source"
    assert (stage_root / "movie.mp4").read_bytes() == b"original"
    assert "origem reapareceu" in tracker.failures[0][1]


def test_process_local_media_keeps_stage_and_reports_failed_rollback(tmp_path, monkeypatch):
    source_root = tmp_path / "source"
    source_root.mkdir()
    source = source_root / "movie.mp4"
    source.write_bytes(b"original")
    stage_root = tmp_path / "stage"
    validator, tracker, _ = _configure_common(monkeypatch)
    monkeypatch.setattr(downloader, "wait_for_disk_capacity", lambda *_args: None)
    monkeypatch.setattr(downloader, "register_in_nebula_queue", lambda **_kwargs: False)
    real_move = downloader.shutil.move
    move_attempts = 0

    def fail_rollback(source_path, destination_path):
        nonlocal move_attempts
        move_attempts += 1
        if move_attempts == 2:
            raise OSError("destination unavailable")
        return real_move(source_path, destination_path)

    monkeypatch.setattr(downloader.shutil, "move", fail_rollback)

    assert not _process(source_root, source, tmp_path / "dest", stage_root, validator, tracker)

    assert not source.exists()
    assert (stage_root / "movie.mp4").read_bytes() == b"original"
    assert "falha ao restaurar origem" in tracker.failures[0][1]
