from __future__ import annotations

import asyncio
import importlib
import logging
import os
import stat
from pathlib import Path
from types import SimpleNamespace

import pytest

main = importlib.import_module("main")


def test_safe_remove_staging_file_only_adds_owner_write_for_readonly_file(
    monkeypatch, tmp_path
):
    target = tmp_path / "readonly.mkv"
    target.write_bytes(b"temporary")
    permissions_before = stat.S_IMODE(os.stat(target).st_mode)
    real_remove = os.remove
    real_chmod = os.chmod
    remove_calls = 0
    chmod_modes = []

    def remove(path):
        nonlocal remove_calls
        remove_calls += 1
        if remove_calls == 1:
            raise PermissionError("simulated read-only file")
        real_remove(path)

    def chmod(path, mode, **kwargs):
        chmod_modes.append(mode)
        real_chmod(path, mode, **kwargs)

    monkeypatch.setattr(main, "is_staging_path", lambda _path: True)
    monkeypatch.setattr(main.os, "remove", remove)
    monkeypatch.setattr(main.os, "chmod", chmod)
    monkeypatch.setattr(main, "_cleanup_empty_parent_dirs", lambda _path: None)

    main.safe_remove_staging_file(target)

    assert not target.exists()
    expected = stat.S_IWRITE if os.name == "nt" else permissions_before | stat.S_IWUSR
    assert chmod_modes == [expected]


def test_make_owner_writable_adds_owner_read_write_and_directory_execute(
    monkeypatch, tmp_path
):
    target = tmp_path / "readonly-dir"
    target.mkdir()
    permissions_before = stat.S_IMODE(os.stat(target).st_mode)
    real_chmod = os.chmod
    chmod_modes = []

    def chmod(path, mode, **kwargs):
        chmod_modes.append(mode)
        real_chmod(path, mode, **kwargs)

    monkeypatch.setattr(main.os, "chmod", chmod)

    main._make_owner_writable(str(target))

    expected = (
        stat.S_IWRITE
        if os.name == "nt"
        else permissions_before | stat.S_IRUSR | stat.S_IWUSR | stat.S_IXUSR
    )
    assert chmod_modes == [expected]


def test_make_owner_writable_restores_owner_access_for_mode_zero_directory(monkeypatch):
    chmod_calls = []

    def chmod(path, mode, **kwargs):
        chmod_calls.append((path, mode, kwargs))

    monkeypatch.setattr(main.os, "name", "posix")
    monkeypatch.setattr(main.os, "lstat", lambda _path: SimpleNamespace(st_mode=stat.S_IFDIR))
    monkeypatch.setattr(main.os, "chmod", chmod)
    monkeypatch.setattr(main.os, "supports_follow_symlinks", {chmod})

    main._make_owner_writable("unreadable-dir")

    assert chmod_calls == [
        ("unreadable-dir", stat.S_IRUSR | stat.S_IWUSR | stat.S_IXUSR, {"follow_symlinks": False})
    ]


def test_make_owner_writable_posix_preserves_other_bits_and_disables_symlink_following(
    monkeypatch, tmp_path
):
    target = tmp_path / "readonly-file"
    target.write_bytes(b"temporary")
    permissions_before = stat.S_IMODE(os.stat(target).st_mode)
    chmod_calls = []

    def chmod(path, mode, **kwargs):
        chmod_calls.append((path, mode, kwargs))

    monkeypatch.setattr(main.os, "chmod", chmod)
    monkeypatch.setattr(main.os, "name", "posix")
    monkeypatch.setattr(main.os, "supports_follow_symlinks", {chmod})

    main._make_owner_writable(str(target))

    assert chmod_calls == [
        (str(target), permissions_before | stat.S_IWUSR, {"follow_symlinks": False})
    ]


def test_make_owner_writable_refuses_symlinks(monkeypatch):
    monkeypatch.setattr(
        main.os,
        "lstat",
        lambda _path: SimpleNamespace(st_mode=stat.S_IFLNK | 0o777),
    )
    monkeypatch.setattr(main.os, "chmod", lambda *_args, **_kwargs: pytest.fail("must not chmod a symlink"))

    with pytest.raises(PermissionError, match="symlink"):
        main._make_owner_writable("link")


def test_safe_remove_does_not_log_success_when_permission_retry_fails(
    monkeypatch, tmp_path, caplog
):
    target = tmp_path / "still-present.mkv"
    target.write_bytes(b"temporary")
    monkeypatch.setattr(main, "is_staging_path", lambda _path: True)
    monkeypatch.setattr(main.os, "remove", lambda _path: (_ for _ in ()).throw(PermissionError()))
    monkeypatch.setattr(
        main,
        "_make_owner_writable",
        lambda _path: (_ for _ in ()).throw(PermissionError("cannot change permissions")),
    )
    monkeypatch.setattr(main, "_cleanup_empty_parent_dirs", lambda _path: pytest.fail("must not cleanup parents"))

    with caplog.at_level(logging.INFO):
        main.safe_remove_staging_file(target)

    assert target.exists()
    assert "Removido arquivo:" not in caplog.text


class AsyncCursor:
    def __init__(self, documents):
        self.documents = iter(documents)

    def __aiter__(self):
        return self

    async def __anext__(self):
        try:
            return next(self.documents)
        except StopIteration:
            raise StopAsyncIteration


class Files:
    def __init__(self, strm_documents, candidates=(), legacy_duplicates=()):
        self.strm_documents = list(strm_documents)
        self.candidates = list(candidates)
        self.legacy_duplicates = list(legacy_duplicates)
        self.find_calls = []
        self.deletes = []
        self.updates = []

    def find(self, query, projection):
        self.find_calls.append((query, projection))
        if "$regex" in query.get("name", {}):
            return AsyncCursor(self.strm_documents)
        if "$not" in query.get("name", {}):
            pattern = query["name"]["$not"]
            excluded_id = query["_id"]["$ne"]
            matches = [
                candidate
                for candidate in self.candidates
                if candidate["_id"] != excluded_id
                and candidate.get("size") == query["size"]
                and not pattern.fullmatch(candidate.get("name", ""))
            ]
            return AsyncCursor(matches)
        return AsyncCursor(self.legacy_duplicates)

    async def delete_one(self, query):
        self.deletes.append(query)

    async def update_one(self, query, update):
        self.updates.append((query, update))


@pytest.mark.asyncio
async def test_cleanup_strm_duplicate_records_removes_only_matching_temporary_mongo_row(tmp_path):
    shared_name = "episode.mkv"
    temporary_id = "0123456789abcdef01234567.mkv"
    strm_documents = [
        {"_id": "duplicate", "name": temporary_id, "local_path": str(tmp_path / "stage-a" / shared_name), "size": 40},
        {"_id": "no-path", "name": "abcdefabcdefabcdefabcdef.mp4", "size": 40},
        {"_id": "orphan", "name": "fedcbafedcbafedcbafedcba.mp4", "local_path": str(tmp_path / "stage-a" / "orphan.mp4"), "size": 41},
    ]
    candidates = [
        {"_id": "real", "name": shared_name, "local_path": str(tmp_path / "stage-b" / shared_name), "size": 40},
        {"_id": "wrong-size", "name": "orphan.mp4", "local_path": str(tmp_path / "other" / "orphan.mp4"), "size": 99},
    ]
    files = Files(strm_documents, candidates)

    await main.cleanup_strm_duplicate_records(SimpleNamespace(files=files))

    assert files.deletes == [{"_id": "duplicate"}]
    assert len(files.find_calls) == 3


@pytest.mark.asyncio
async def test_cleanup_duplicate_target_records_marks_legacy_rows_for_recovery_without_deleting(
    monkeypatch,
):
    monkeypatch.setattr(main.time, "time", lambda: 1234)
    files = Files(
        [],
        legacy_duplicates=[
            {"_id": "legacy-1", "name": "Movie.mkv", "local_path": "D:/stage/Movie.mkv", "parent": "/Films"},
            {"_id": "legacy-2", "name": "Episode.mkv", "local_path": "D:/stage/Episode.mkv", "parent": "/Series/Show"},
        ],
    )

    await main.cleanup_duplicate_target_records(SimpleNamespace(files=files))

    assert files.updates == [
        ({"_id": "legacy-1"}, {"$set": {"failed_reason": "duplicate_target_preserved", "preserved_at": 1234}}),
        ({"_id": "legacy-2"}, {"$set": {"failed_reason": "duplicate_target_preserved", "preserved_at": 1234}}),
    ]
    assert files.deletes == []


@pytest.mark.asyncio
async def test_garbage_collector_deletes_only_old_failed_and_orphaned_staging_records(
    monkeypatch, tmp_path
):
    existing_staging = tmp_path / "still-present.mkv"
    existing_staging.write_bytes(b"keep")
    files_on_db = [
        {"_id": "old-orphan", "local_path": str(tmp_path / "missing-old.mkv"), "staged_at": 9000},
        {"_id": "fresh-orphan", "local_path": str(tmp_path / "missing-fresh.mkv"), "staged_at": 9950},
        {"_id": "existing-file", "local_path": str(existing_staging), "staged_at": 9000},
        {"_id": "empty-path", "local_path": "", "staged_at": 9000},
    ]

    class Files:
        def __init__(self):
            self.delete_calls = []
            self.find_calls = []

        async def delete_many(self, query):
            self.delete_calls.append(query)
            return SimpleNamespace(deleted_count=2 if len(self.delete_calls) == 1 else 1)

        def find(self, query, projection):
            self.find_calls.append((query, projection))
            return AsyncCursor(files_on_db)

    files = Files()
    mongo = SimpleNamespace(files=files)
    duplicate_cleanup_calls = []

    async def cleanup_duplicates(database):
        duplicate_cleanup_calls.append(database)

    async def stop_after_cycle(_interval):
        raise asyncio.CancelledError

    monkeypatch.setenv("GC_INTERVAL", "7")
    monkeypatch.setenv("GC_STALE_AGE", "100")
    monkeypatch.setattr(main.time, "time", lambda: 10000)
    monkeypatch.setattr(main, "cleanup_duplicate_target_records", cleanup_duplicates)
    monkeypatch.setattr(main.asyncio, "sleep", stop_after_cycle)

    with pytest.raises(asyncio.CancelledError):
        await main.garbage_collector(mongo)

    assert files.delete_calls == [
        {"type": "file", "status": "failed", "failed_at": {"$lt": 9900}},
        {"_id": {"$in": ["old-orphan"]}},
    ]
    assert files.find_calls == [(
        {"type": "file", "status": "staging", "local_path": {"$exists": True}},
        {"_id": 1, "local_path": 1, "staged_at": 1},
    )]
    assert duplicate_cleanup_calls == [mongo]
    assert existing_staging.read_bytes() == b"keep"


@pytest.mark.asyncio
async def test_garbage_collector_keeps_running_after_database_failure(monkeypatch):
    class Files:
        async def delete_many(self, _query):
            raise ConnectionError("Mongo unavailable")

    async def stop_after_attempt(_interval):
        raise asyncio.CancelledError

    monkeypatch.setattr(main.asyncio, "sleep", stop_after_attempt)
    with pytest.raises(asyncio.CancelledError):
        await main.garbage_collector(SimpleNamespace(files=Files()))
