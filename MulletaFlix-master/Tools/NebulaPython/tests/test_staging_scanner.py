from __future__ import annotations

import asyncio
import importlib
from types import SimpleNamespace

import pytest
from pymongo.errors import DuplicateKeyError

main = importlib.import_module("main")


class Files:
    def __init__(self, existing=None, *, insert_error=None):
        self.existing = existing
        self.insert_error = insert_error
        self.updates = []
        self.deletes = []
        self.inserts = []

    async def find_one(self, _query):
        return self.existing

    async def update_one(self, query, update, **kwargs):
        self.updates.append((query, update, kwargs))

    async def delete_one(self, query):
        self.deletes.append(query)

    async def insert_one(self, document):
        self.inserts.append(document)
        if self.insert_error:
            raise self.insert_error


async def _stop_after_scan(_seconds):
    raise asyncio.CancelledError


def _configure(monkeypatch, files):
    mongo = SimpleNamespace(files=files)
    monkeypatch.setattr(main, "resolve_media_parent", _parent)
    monkeypatch.setattr(main.asyncio, "sleep", _stop_after_scan)
    return mongo


async def _parent(_mongo, _parent, _filename):
    return "/raphael/Filmes"


@pytest.mark.asyncio
async def test_staging_scanner_registers_new_media_without_enqueuing_it(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "new-movie.mkv"
    stage.mkdir()
    media.write_bytes(b"video")
    files = Files()
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert len(files.inserts) == 1
    saved = files.inserts[0]
    assert saved["type"] == "file"
    assert saved["name"] == "new-movie.mkv"
    assert saved["parent"] == "/raphael/Filmes"
    assert saved["status"] == "queued"
    assert saved["local_path"] == str(media)
    assert saved["size"] == 5
    assert saved["parts"] == []


@pytest.mark.asyncio
async def test_staging_scanner_updates_existing_active_file_location(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "episode.mp4"
    stage.mkdir()
    media.write_bytes(b"episode")
    files = Files(existing={
        "_id": "queued-id",
        "status": "queued",
        "local_path": "Z:/old-stage/episode.mp4",
        "recovery_required": True,
    })
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert files.updates == [({"_id": "queued-id"}, {"$set": {
        "local_path": str(media), "size": 7, "recovery_required": False,
    }}, {})]
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_removes_file_already_completed(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "done.mkv"
    stage.mkdir()
    media.write_bytes(b"done")
    files = Files(existing={"_id": "done-id", "status": "completed"})
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "STAGING_DIRS", [str(stage)])

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert not media.exists()
    assert files.inserts == []
    assert files.updates == []


@pytest.mark.asyncio
async def test_staging_scanner_reactivates_failed_record(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "retry.mkv"
    stage.mkdir()
    media.write_bytes(b"retry")
    files = Files(existing={"_id": "retry-id", "status": "failed", "failed_reason": "network"})
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    query, update, _kwargs = files.updates[0]
    assert query == {"_id": "retry-id"}
    assert update["$set"]["status"] == "queued"
    assert update["$set"]["local_path"] == str(media)
    assert update["$set"]["size"] == 5
    assert update["$set"]["failed_reason"] is None
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_preserves_duplicate_stage_record_but_deletes_local_copy(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "duplicate.mkv"
    stage.mkdir()
    media.write_bytes(b"copy")
    files = Files(
        existing={"_id": "duplicate-id", "status": "failed", "failed_reason": "duplicate_target"}
    )
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "STAGING_DIRS", [str(stage)])

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert not media.exists()
    assert files.deletes == [{"_id": "duplicate-id"}]
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_duplicate_insert_removes_untracked_local_copy(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "collision.mp4"
    stage.mkdir()
    media.write_bytes(b"collision")
    files = Files(insert_error=DuplicateKeyError("unique target collision"))
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "STAGING_DIRS", [str(stage)])

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert len(files.inserts) == 1
    assert not media.exists()
