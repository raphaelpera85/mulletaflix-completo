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
        self.queries = []
        self.updates = []
        self.deletes = []
        self.inserts = []

    async def find_one(self, query):
        self.queries.append(query)
        if not self.existing:
            return None
        for clause in query.get("$or", []):
            if "local_path" in clause and clause["local_path"] == self.existing.get("local_path"):
                return self.existing
            if clause.get("name") != self.existing.get("name"):
                continue
            if clause.get("parent") != self.existing.get("parent"):
                continue
            status = clause.get("status")
            if status == self.existing.get("status") or (
                isinstance(status, dict) and self.existing.get("status") in status.get("$in", [])
            ):
                return self.existing
        return None

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
        "name": "episode.mp4",
        "parent": "/raphael/Filmes",
        "status": "queued",
        "local_path": "Z:/old-stage/episode.mp4",
        "recovery_required": True,
    })
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert files.updates == [({"_id": "queued-id"}, {"$set": {
        "local_path": str(media), "queue_identity": main.queue_identity(str(media)),
        "size": 7, "recovery_required": False,
    }}, {})]
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_removes_file_already_completed(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "done.mkv"
    stage.mkdir()
    media.write_bytes(b"done")
    files = Files(existing={
        "_id": "done-id",
        "name": "done.mkv",
        "parent": "/raphael/Filmes",
        "status": "completed",
    })
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "STAGING_DIRS", [str(stage)])

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert not media.exists()
    assert files.inserts == []
    assert files.updates == []


@pytest.mark.asyncio
async def test_staging_scanner_does_not_match_completed_same_name_in_another_parent(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "episode.mkv"
    stage.mkdir()
    media.write_bytes(b"episode")
    files = Files(existing={
        "_id": "other-library-id",
        "name": "episode.mkv",
        "parent": "/raphael/Series/Another Show/Season 01",
        "status": "completed",
    })
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert media.exists()
    assert len(files.inserts) == 1
    assert files.inserts[0]["parent"] == "/raphael/Filmes"


@pytest.mark.asyncio
async def test_staging_scanner_reactivates_failed_record(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "retry.mkv"
    stage.mkdir()
    media.write_bytes(b"retry")
    files = Files(existing={
        "_id": "retry-id",
        "name": "retry.mkv",
        "parent": "/raphael/Filmes",
        "status": "failed",
        "failed_reason": "network",
    })
    mongo = _configure(monkeypatch, files)

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    query, update, _kwargs = next(entry for entry in files.updates if "status" in entry[1].get("$set", {}))
    assert query == {"_id": "retry-id"}
    assert update["$set"]["status"] == "queued"
    assert update["$set"]["local_path"] == str(media)
    assert update["$set"]["queue_identity"] == main.queue_identity(str(media))
    assert update["$set"]["size"] == 5
    assert update["$set"]["failed_reason"] is None
    assert files.inserts == []


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "failed_reason",
    [
        "telegram_reconciliation_inconclusive: search unavailable",
        "upload_checkpoint_unavailable: write failed",
    ],
)
@pytest.mark.parametrize("status,deadline_field", [("retry_wait", "retry_after"), ("retry_wait", "next_retry_at"), ("failed", "next_retry_at")])
async def test_staging_scanner_waits_for_deferred_upload_retry_window(monkeypatch, tmp_path, failed_reason, status, deadline_field):
    stage = tmp_path / "stage"
    media = stage / "ambiguous.mkv"
    stage.mkdir()
    media.write_bytes(b"retry")
    files = Files(existing={
        "_id": "ambiguous-id",
        "name": "ambiguous.mkv",
        "parent": "/raphael/Filmes",
        "status": status,
        "failed_reason": failed_reason,
        deadline_field: 200,
    })
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 199))

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert files.updates == []
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_honours_later_retry_deadline_when_both_fields_exist(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "ambiguous.mkv"
    stage.mkdir()
    media.write_bytes(b"retry")
    files = Files(existing={
        "_id": "ambiguous-id",
        "name": "ambiguous.mkv",
        "parent": "/raphael/Filmes",
        "status": "retry_wait",
        "failed_reason": "upload_checkpoint_unavailable: write failed",
        "next_retry_at": 200,
        "retry_after": 250,
    })
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 220))

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    assert files.updates == []
    assert files.inserts == []


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "failed_reason",
    [
        "telegram_reconciliation_inconclusive: search unavailable",
        "upload_checkpoint_unavailable: write failed",
    ],
)
@pytest.mark.parametrize("status,deadline_field", [("retry_wait", "retry_after"), ("retry_wait", "next_retry_at"), ("failed", "next_retry_at")])
async def test_staging_scanner_requeues_deferred_upload_after_delay(monkeypatch, tmp_path, failed_reason, status, deadline_field):
    stage = tmp_path / "stage"
    media = stage / "ambiguous.mkv"
    stage.mkdir()
    media.write_bytes(b"retry")
    files = Files(existing={
        "_id": "ambiguous-id",
        "name": "ambiguous.mkv",
        "parent": "/raphael/Filmes",
        "status": status,
        "failed_reason": failed_reason,
        deadline_field: 200,
    })
    mongo = _configure(monkeypatch, files)
    monkeypatch.setattr(main, "time", SimpleNamespace(time=lambda: 200))

    with pytest.raises(asyncio.CancelledError):
        await main.staging_scanner(mongo, [str(stage)])

    query, update, _kwargs = next(entry for entry in files.updates if "status" in entry[1].get("$set", {}))
    assert query["_id"] == "ambiguous-id"
    assert query["status"] == {"$in": ["failed", "retry_wait"]}
    assert query["failed_reason"] == failed_reason
    assert query["$and"] == [
        {"$or": [{"retry_after": {"$exists": False}}, {"retry_after": {"$lte": 200}}]},
        {"$or": [{"next_retry_at": {"$exists": False}}, {"next_retry_at": {"$lte": 200}}]},
    ]
    assert update["$set"]["status"] == "queued"
    assert update["$set"]["failed_reason"] is None
    assert update["$unset"] == {"next_retry_at": 1, "retry_after": 1}
    assert files.inserts == []


@pytest.mark.asyncio
async def test_staging_scanner_preserves_duplicate_stage_record_but_deletes_local_copy(monkeypatch, tmp_path):
    stage = tmp_path / "stage"
    media = stage / "duplicate.mkv"
    stage.mkdir()
    media.write_bytes(b"copy")
    files = Files(
        existing={
            "_id": "duplicate-id",
            "name": "duplicate.mkv",
            "parent": "/raphael/Filmes",
            "status": "failed",
            "failed_reason": "duplicate_target",
        }
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
