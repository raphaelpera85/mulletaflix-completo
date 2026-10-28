from __future__ import annotations

import json
import uuid

import pytest

import upload_checkpoint
from upload_checkpoint import UploadCheckpointJournal


def _metadata(file_uuid: str, *, part_id: int = 0, size: int = 4) -> dict:
    return {
        "part_id": part_id,
        "tg_file": f"telegram-file-{part_id}",
        "tg_message": 91 + part_id,
        "file_size": size,
        "source_sha256": "a" * 64,
        "chunk_name": f"{file_uuid}.part_{part_id:03d}",
        "byte_start": part_id * 4,
        "byte_end": part_id * 4 + size - 1,
        "caption": f"[NEBULA] UUID: {file_uuid} | PARTE: {part_id + 1}/1",
        "bot_index": 0,
        "bot_name": "bot-a",
    }


def test_pending_intent_survives_process_restart(tmp_path):
    file_uuid = str(uuid.uuid4())
    first_process = UploadCheckpointJournal(tmp_path)
    first_process.mark_pending(file_uuid, 8, 1234, 2, 4, 1, "b" * 64)

    recovered = UploadCheckpointJournal(tmp_path).load(file_uuid, 8, 1234, 2, 4)

    assert recovered.parts == ()
    assert recovered.pending_parts == frozenset({1})
    assert recovered.pending_sha256 == {1: "b" * 64}


def test_confirmed_part_replaces_pending_intent_durably(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    journal.mark_pending(file_uuid, 8, 1234, 2, 4, 1, "b" * 64)
    part = _metadata(file_uuid, part_id=1)

    journal.save_part(file_uuid, 8, 1234, 2, 4, part)
    recovered = UploadCheckpointJournal(tmp_path).load(file_uuid, 8, 1234, 2, 4)

    assert recovered.parts == (part,)
    assert recovered.pending_parts == frozenset()


def test_changed_source_fingerprint_requires_new_generation(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    journal.mark_pending(file_uuid, 8, 1234, 2, 4, 0, "b" * 64)

    snapshot = UploadCheckpointJournal(tmp_path).load(file_uuid, 9, 1234, 3, 4)
    assert snapshot.requires_new_generation is True


def test_corrupt_checkpoint_fails_closed_without_silent_reset(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    journal.mark_pending(file_uuid, 8, 1234, 2, 4, 0, "b" * 64)
    (tmp_path / f"{uuid.UUID(file_uuid).hex}.json").write_text("{broken", encoding="utf-8")

    with pytest.raises(ValueError, match="corrompido"):
        journal.load(file_uuid, 8, 1234, 2, 4)


def test_checkpoint_is_atomic_json_with_no_source_path_or_credentials(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    journal.mark_pending(file_uuid, 8, 1234, 2, 4, 0, "b" * 64)

    document = json.loads((tmp_path / f"{uuid.UUID(file_uuid).hex}.json").read_text(encoding="utf-8"))

    assert document["file_uuid"] == file_uuid
    assert document["pending_part_numbers"] == [0]
    assert "local_path" not in document
    assert "token" not in json.dumps(document).lower()
    assert list(tmp_path.glob("*.tmp")) == []


def test_legacy_v1_checkpoint_requires_new_upload_generation(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    path = tmp_path / f"{uuid.UUID(file_uuid).hex}.json"
    path.write_text(json.dumps({
        "format_version": 1,
        "file_uuid": file_uuid,
        "source_size": 8,
        "source_mtime_ns": 1234,
        "total_parts": 2,
        "chunk_size": 4,
        "parts": [_metadata(file_uuid)],
        "pending_part_numbers": [],
    }), encoding="utf-8")

    recovered = journal.load(file_uuid, 8, 1234, 2, 4)

    assert recovered.requires_new_generation is True
    assert recovered.parts == ()


@pytest.mark.parametrize("failure_stage", ["fsync", "replace"])
def test_checkpoint_write_failure_preserves_previous_checkpoint_and_cleans_temporary_file(
    tmp_path, monkeypatch, failure_stage
):
    file_uuid = str(uuid.uuid4())
    journal = UploadCheckpointJournal(tmp_path)
    first_part = _metadata(file_uuid, part_id=0)
    journal.save_part(file_uuid, 8, 1234, 2, 4, first_part)
    checkpoint_path = tmp_path / f"{uuid.UUID(file_uuid).hex}.json"
    previous_checkpoint = checkpoint_path.read_bytes()

    def fail_checkpoint_write(*_args, **_kwargs):
        raise OSError(f"injected {failure_stage} failure")

    monkeypatch.setattr(upload_checkpoint.os, failure_stage, fail_checkpoint_write)

    with pytest.raises(OSError, match=f"injected {failure_stage} failure"):
        journal.save_part(file_uuid, 8, 1234, 2, 4, _metadata(file_uuid, part_id=1))

    assert checkpoint_path.read_bytes() == previous_checkpoint
    recovered = journal.load(file_uuid, 8, 1234, 2, 4)
    assert recovered.parts == (first_part,)
    assert recovered.pending_parts == frozenset()
    assert list(tmp_path.glob("*.tmp")) == []
