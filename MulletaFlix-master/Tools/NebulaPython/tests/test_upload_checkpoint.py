from __future__ import annotations

import asyncio
import ctypes
import importlib
import os
import uuid
from types import SimpleNamespace

import pytest

import upload_checkpoint as checkpoint

main = importlib.import_module("main")


def _part(file_uuid: str, payload: bytes, part_number: int = 0, chunk_size: int = 8):
    start = part_number * chunk_size
    return {
        "part_id": part_number,
        "tg_file": f"telegram-file-{part_number}",
        "tg_message": 100 + part_number,
        "file_size": len(payload),
        "chunk_name": f"{file_uuid}.part_{part_number:03d}",
        "byte_start": start,
        "byte_end": start + len(payload) - 1,
        "bot_index": 0,
        "source_sha256": checkpoint.compute_part_sha256(payload),
    }


def test_journal_persists_pending_part_and_acknowledgement_atomically(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = checkpoint.UploadCheckpointJournal(tmp_path)
    payload = b"payload"

    journal.mark_pending(file_uuid, len(payload), 1234, 1, 8, 0, checkpoint.compute_part_sha256(payload))
    pending = journal.load(file_uuid, len(payload), 1234, 1, 8)
    assert pending.pending_parts == frozenset({0})

    journal.save_part(file_uuid, len(payload), 1234, 1, 8, _part(file_uuid, payload))
    saved = journal.load(file_uuid, len(payload), 1234, 1, 8)
    assert saved.pending_parts == frozenset()
    assert saved.parts[0]["tg_message"] == 100
    assert saved.parts[0]["source_sha256"] == checkpoint.compute_part_sha256(payload)
    assert [path.suffix for path in tmp_path.iterdir()] == [".json"]


@pytest.mark.skipif(os.name == "nt", reason="The POSIX replacement branch syncs the directory after rename.")
def test_checkpoint_replacement_syncs_parent_directory_after_rename(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())
    events = []
    replace = checkpoint.os.replace

    def tracked_replace(source, destination):
        replace(source, destination)
        events.append("replace")

    def tracked_sync(directory):
        events.append(("sync-directory", directory))

    monkeypatch.setattr(checkpoint.os, "replace", tracked_replace)
    monkeypatch.setattr(checkpoint, "_sync_directory", tracked_sync)

    checkpoint.UploadCheckpointJournal(tmp_path).mark_pending(
        file_uuid, 7, 1234, 1, 8, 0, checkpoint.compute_part_sha256(b"payload")
    )

    assert events == ["replace", ("sync-directory", tmp_path)]


@pytest.mark.skipif(os.name == "nt", reason="Windows uses MoveFileExW with MOVEFILE_WRITE_THROUGH.")
def test_checkpoint_directory_sync_failure_is_reported_after_replace(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = checkpoint.UploadCheckpointJournal(tmp_path)

    def fail_sync(_directory):
        raise OSError("simulated directory sync failure")

    monkeypatch.setattr(checkpoint, "_sync_directory", fail_sync)

    with pytest.raises(OSError, match="simulated directory sync failure"):
        journal.mark_pending(
            file_uuid, 7, 1234, 1, 8, 0, checkpoint.compute_part_sha256(b"payload")
        )

    assert journal._get_path(file_uuid).exists()


@pytest.mark.skipif(os.name != "nt", reason="MoveFileExW is Windows-specific.")
def test_checkpoint_replace_uses_movefileex_write_through(monkeypatch, tmp_path):
    calls = []
    dll_loads = []

    def move_file(source, destination, flags):
        calls.append((source, destination, flags))
        return 1

    def load_kernel32(name, use_last_error):
        dll_loads.append((name, use_last_error))
        return SimpleNamespace(MoveFileExW=move_file)

    monkeypatch.setattr(
        ctypes,
        "WinDLL",
        load_kernel32,
    )

    source = tmp_path / "checkpoint.tmp"
    destination = tmp_path / "checkpoint.json"
    checkpoint._replace_checkpoint_file(source, destination)

    assert dll_loads == [("kernel32", True)]
    assert calls == [(str(source), str(destination), 0x1 | 0x8)]
    assert move_file.argtypes == [ctypes.wintypes.LPCWSTR, ctypes.wintypes.LPCWSTR, ctypes.wintypes.DWORD]
    assert move_file.restype == ctypes.wintypes.BOOL


@pytest.mark.skipif(os.name != "nt", reason="MoveFileExW is Windows-specific.")
def test_checkpoint_replace_reports_movefileex_failure(monkeypatch, tmp_path):
    def move_file(_source, _destination, _flags):
        return 0

    monkeypatch.setattr(
        ctypes,
        "WinDLL",
        lambda _name, use_last_error: SimpleNamespace(MoveFileExW=move_file),
    )
    monkeypatch.setattr(ctypes, "get_last_error", lambda: 5)
    monkeypatch.setattr(ctypes, "WinError", lambda error: OSError(f"Windows error {error}"))

    with pytest.raises(OSError, match="Windows error 5"):
        checkpoint._replace_checkpoint_file(tmp_path / "checkpoint.tmp", tmp_path / "checkpoint.json")


def test_journal_rejects_changed_source_and_corrupt_part_metadata(tmp_path):
    file_uuid = str(uuid.uuid4())
    journal = checkpoint.UploadCheckpointJournal(tmp_path)
    payload = b"payload"
    journal.mark_pending(file_uuid, len(payload), 1234, 1, 8, 0, checkpoint.compute_part_sha256(payload))

    changed = journal.load(file_uuid, len(payload), 1235, 1, 8)
    assert changed.requires_new_generation is True
    with pytest.raises(ValueError, match="intenção de parte"):
        journal.mark_pending(file_uuid, len(payload), 1234, 1, 8, 0, "invalid")


@pytest.mark.asyncio
async def test_telegram_history_requires_exact_uuid_part_digest_and_complete_search():
    file_uuid = str(uuid.uuid4())
    digest = checkpoint.compute_part_sha256(b"payload")
    caption = f"[NEBULA] TIPO: movie | MIDIA: Title | PARTE: 1/1 | UUID: {file_uuid} | SHA256: {digest}"
    expected_message = SimpleNamespace(
        id=456,
        caption=caption,
        document=SimpleNamespace(file_id="telegram-file"),
        from_user=SimpleNamespace(id=99),
    )
    bot = SimpleNamespace(
        name="bot-a",
        me=SimpleNamespace(id=99),
        search_messages=lambda *_args, **_kwargs: [expected_message],
    )

    found = await main.find_uploaded_telegram_part(
        [bot], -100, file_uuid, 0, 1, source_sha256=digest
    )
    assert found.status is main.TelegramPartReconciliationStatus.FOUND
    assert found.message.id == 456
    assert found.message._nebula_bot_index == 0

    wrong_part = SimpleNamespace(
        id=457,
        caption=caption.replace("PARTE: 1/1", "PARTE: 2/2"),
        document=SimpleNamespace(file_id="other-file"),
    )
    wrong_bot = SimpleNamespace(search_messages=lambda *_args, **_kwargs: [wrong_part])
    absent = await main.find_uploaded_telegram_part(
        [wrong_bot], -100, file_uuid, 0, 1, source_sha256=digest
    )
    assert absent.status is main.TelegramPartReconciliationStatus.NOT_FOUND


@pytest.mark.asyncio
async def test_telegram_history_search_errors_are_inconclusive_not_absence():
    file_uuid = str(uuid.uuid4())
    digest = checkpoint.compute_part_sha256(b"payload")

    async def unavailable(*_args, **_kwargs):
        raise TimeoutError("search timed out")

    bot = SimpleNamespace(search_messages=unavailable)
    result = await main.find_uploaded_telegram_part(
        [bot], -100, file_uuid, 0, 1, source_sha256=digest
    )
    assert result.status is main.TelegramPartReconciliationStatus.INCONCLUSIVE


@pytest.mark.asyncio
async def test_telegram_history_checks_every_bot_in_the_pool():
    file_uuid = str(uuid.uuid4())
    digest = checkpoint.compute_part_sha256(b"payload")
    caption = f"[NEBULA] TIPO: movie | MIDIA: Title | PARTE: 1/1 | UUID: {file_uuid} | SHA256: {digest}"
    found_message = SimpleNamespace(
        id=678,
        caption=caption,
        document=SimpleNamespace(file_id="telegram-file"),
        from_user=SimpleNamespace(id=106),
    )
    bots = []
    for number in range(100, 106):
        messages = [found_message] if number == 105 else []
        bots.append(SimpleNamespace(
            name=f"bot-{number}",
            me=SimpleNamespace(id=number),
            search_messages=lambda *_args, messages=messages, **_kwargs: messages,
        ))

    result = await main.find_uploaded_telegram_part(
        bots, -100, file_uuid, 0, 1, source_sha256=digest
    )

    assert result.status is main.TelegramPartReconciliationStatus.FOUND
    assert result.message.id == 678
    assert result.message._nebula_bot_index == 5


@pytest.mark.asyncio
async def test_telegram_history_searches_past_first_page_for_accepted_part():
    file_uuid = str(uuid.uuid4())
    digest = checkpoint.compute_part_sha256(b"payload")
    caption = f"[NEBULA] TIPO: movie | MIDIA: Title | PARTE: 1/1 | UUID: {file_uuid} | SHA256: {digest}"
    messages = [
        SimpleNamespace(
            id=index,
            caption=f"UUID: {file_uuid} | PARTE: 2/2 | SHA256: {digest}",
            document=SimpleNamespace(file_id=f"other-{index}"),
        )
        for index in range(main.TELEGRAM_RECONCILIATION_PAGE_SIZE)
    ]
    accepted = SimpleNamespace(
        id=999,
        caption=caption,
        document=SimpleNamespace(file_id="accepted-file"),
    )
    messages.append(accepted)
    calls = []

    def search_messages(_chat_id, *, query, offset, limit):
        calls.append((query, offset, limit))
        return messages[offset : offset + limit]

    bot = SimpleNamespace(search_messages=search_messages)
    result = await main.find_uploaded_telegram_part(
        [bot], -100, file_uuid, 0, 1, source_sha256=digest
    )

    assert result.status is main.TelegramPartReconciliationStatus.FOUND
    assert result.message.id == accepted.id
    assert [call[1] for call in calls] == [0, main.TELEGRAM_RECONCILIATION_PAGE_SIZE]


@pytest.mark.asyncio
async def test_telegram_history_search_limit_is_inconclusive_not_absence(monkeypatch):
    monkeypatch.setattr(main, "TELEGRAM_RECONCILIATION_MAX_MESSAGES", 100)
    file_uuid = str(uuid.uuid4())
    digest = checkpoint.compute_part_sha256(b"payload")
    calls = []

    def search_messages(_chat_id, *, offset, limit, **_kwargs):
        calls.append(offset)
        return [SimpleNamespace(id=offset + i) for i in range(limit)]

    bot = SimpleNamespace(search_messages=search_messages)
    result = await main.find_uploaded_telegram_part(
        [bot], -100, file_uuid, 0, 1, source_sha256=digest
    )

    assert result.status is main.TelegramPartReconciliationStatus.INCONCLUSIVE
    assert calls == [0, 50]


@pytest.mark.asyncio
async def test_reconciliation_waits_through_visibility_window_before_retry(monkeypatch):
    waits = []

    async def absent(*_args, **_kwargs):
        return main.TelegramPartReconciliation(main.TelegramPartReconciliationStatus.NOT_FOUND)

    async def record_wait(seconds):
        waits.append(seconds)

    monkeypatch.setattr(main, "find_uploaded_telegram_part", absent)
    monkeypatch.setattr(main.asyncio, "sleep", record_wait)

    result = await main.reconcile_telegram_part_with_retries(
        [object()], -100, str(uuid.uuid4()), 0, 1,
        source_sha256=checkpoint.compute_part_sha256(b"payload"),
    )

    assert result.status is main.TelegramPartReconciliationStatus.NOT_FOUND
    assert waits == [1, 2, 4, 8, 16]


@pytest.mark.asyncio
async def test_accepted_telegram_send_with_lost_checkpoint_is_reconciled_on_automatic_retry(
    monkeypatch, tmp_path,
):
    file_uuid = str(uuid.uuid4())
    payload = b"payload"
    digest = checkpoint.compute_part_sha256(payload)
    journal = checkpoint.UploadCheckpointJournal(tmp_path / "journal")
    journal.mark_pending(file_uuid, len(payload), 1234, 1, len(payload), 0, digest)
    accepted_message = SimpleNamespace(
        id=987,
        document=SimpleNamespace(file_id="accepted-file-id"),
        _nebula_bot_index=0,
        _nebula_bot_name="bot-a",
    )
    bots = [SimpleNamespace(name="bot-a")]
    send_calls = []
    acknowledgement_writes = 0

    async def send(*_args, **_kwargs):
        send_calls.append(True)
        return accepted_message

    async def fail_first_acknowledgement(part):
        nonlocal acknowledgement_writes
        acknowledgement_writes += 1
        if acknowledgement_writes == 1:
            raise main.UploadCheckpointUnavailableError(
                "upload_checkpoint_unavailable: simulated lost acknowledgement"
            )
        journal.save_part(file_uuid, len(payload), 1234, 1, len(payload), part)

    async def found_in_history():
        return main.TelegramPartReconciliation(
            main.TelegramPartReconciliationStatus.FOUND, accepted_message
        )

    monkeypatch.setattr(main, "next_upload_bot_index", lambda _count: 0)
    monkeypatch.setattr(main, "_send_part_on_idle_bot", send)

    with pytest.raises(main.UploadCheckpointUnavailableError):
        await main.upload_part_with_retries(
            1, bots, -100, "unused", file_uuid, 0, payload,
            on_success=fail_first_acknowledgement,
        )

    pending = journal.load(file_uuid, len(payload), 1234, 1, len(payload))
    assert pending.pending_parts == frozenset({0})
    assert main.is_deferred_upload_retry({
        "failed_reason": "upload_checkpoint_unavailable: simulated lost acknowledgement",
    })

    recovered = await main.upload_part_with_retries(
        1, bots, -100, "unused", file_uuid, 0, payload,
        reconcile=found_in_history,
        on_success=fail_first_acknowledgement,
        reconcile_before_send=True,
    )

    assert len(send_calls) == 1
    assert recovered["tg_message"] == accepted_message.id
    assert journal.load(file_uuid, len(payload), 1234, 1, len(payload)).parts[0]["tg_file"] == "accepted-file-id"


@pytest.mark.asyncio
async def test_resume_uses_durable_ack_when_mongo_progress_checkpoint_was_lost(
    monkeypatch, tmp_path,
):
    file_uuid = str(uuid.uuid4())
    payload = b"mongo checkpoint lost"
    media = tmp_path / "mongo-checkpoint-lost.bin"
    media.write_bytes(payload)
    journal = checkpoint.UploadCheckpointJournal(tmp_path / "journal")
    source_mtime_ns = media.stat().st_mtime_ns
    digest = checkpoint.compute_part_sha256(payload)
    part = {
        "part_id": 0,
        "tg_file": "accepted-file-id",
        "tg_message": 654,
        "file_size": len(payload),
        "chunk_name": f"{file_uuid}.part_000",
        "byte_start": 0,
        "byte_end": len(payload) - 1,
        "bot_index": 0,
        "source_sha256": digest,
    }
    journal.save_part(file_uuid, len(payload), source_mtime_ns, 1, 64, part)
    monkeypatch.setattr(main, "CHUNK_SIZE", 64)
    monkeypatch.setattr(main, "UPLOAD_CHECKPOINT_JOURNAL", journal)
    file_doc = {"_id": "mongo-lost-ack", "obfuscated_id": file_uuid, "parts": []}

    resumed_uuid, total_parts, parts, pending, *_ = await main._prepare_upload_resume(
        SimpleNamespace(), file_doc, str(media), len(payload), chunk_size=64
    )

    assert resumed_uuid == file_uuid
    assert total_parts == 1
    assert [item["tg_message"] for item in parts] == [654]
    assert pending == set()


@pytest.mark.asyncio
async def test_legacy_mongo_progress_is_upgraded_without_resending_valid_parts(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())
    media = tmp_path / "legacy.bin"
    payload = b"legacy payload"
    media.write_bytes(payload)
    old_part = {
        "part_id": 0,
        "tg_file": "telegram-file",
        "tg_message": 321,
        "file_size": len(payload),
        "chunk_name": f"{file_uuid}.part_000",
        "bot_name": "bot-a",
    }
    file_doc = {
        "_id": "legacy",
        "worker_id": "worker-1",
        "lease_id": "lease-1",
        "lease_until": 9999999999,
        "obfuscated_id": file_uuid,
        "size": len(payload),
        "mtime": media.stat().st_mtime,
        "parts": [old_part],
    }
    journal = checkpoint.UploadCheckpointJournal(tmp_path / "journal")
    monkeypatch.setattr(main, "UPLOAD_CHECKPOINT_JOURNAL", journal)

    resumed_uuid, total_parts, parts, pending, _journal, _mtime_ns = await main._prepare_upload_resume(
        SimpleNamespace(),
        file_doc,
        str(media),
        len(payload),
        chunk_size=64,
    )

    digest = checkpoint.compute_part_sha256(payload)
    assert resumed_uuid == file_uuid
    assert total_parts == 1
    assert pending == set()
    assert parts[0]["tg_file"] == "telegram-file"
    assert parts[0]["source_sha256"] == digest
    assert parts[0]["byte_start"] == 0
    assert parts[0]["byte_end"] == len(payload) - 1
    assert journal.load(file_uuid, len(payload), media.stat().st_mtime_ns, 1, 64).parts[0]["source_sha256"] == digest


@pytest.mark.asyncio
async def test_checkpoint_read_failure_defers_upload_instead_of_rotating_uuid(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())

    class UnavailableJournal:
        def load(self, *_args, **_kwargs):
            raise PermissionError("journal is unavailable")

    monkeypatch.setattr(main, "UPLOAD_CHECKPOINT_JOURNAL", UnavailableJournal())
    media = tmp_path / "checkpoint-unavailable.bin"
    media.write_bytes(b"payload")
    file_doc = {"_id": "item", "obfuscated_id": file_uuid, "parts": []}

    with pytest.raises(main.UploadCheckpointUnavailableError):
        await main._prepare_upload_resume(
            SimpleNamespace(), file_doc, str(media), media.stat().st_size, chunk_size=64
        )

    assert file_doc["obfuscated_id"] == file_uuid


@pytest.mark.asyncio
async def test_corrupt_checkpoint_defers_upload_instead_of_rotating_uuid(monkeypatch, tmp_path):
    file_uuid = str(uuid.uuid4())
    media = tmp_path / "corrupt-checkpoint.bin"
    media.write_bytes(b"payload")
    journal_dir = tmp_path / "journal"
    journal_dir.mkdir()
    (journal_dir / f"{uuid.UUID(file_uuid).hex}.json").write_text("{invalid", encoding="utf-8")
    journal = checkpoint.UploadCheckpointJournal(journal_dir)
    monkeypatch.setattr(main, "UPLOAD_CHECKPOINT_JOURNAL", journal)
    file_doc = {"_id": "corrupt-journal", "obfuscated_id": file_uuid, "parts": []}

    with pytest.raises(main.UploadCheckpointUnavailableError, match="journal está corrompido"):
        await main._prepare_upload_resume(
            SimpleNamespace(), file_doc, str(media), media.stat().st_size, chunk_size=64
        )

    assert file_doc["obfuscated_id"] == file_uuid


@pytest.mark.parametrize(
    "reason",
    [
        "telegram_reconciliation_inconclusive: search unavailable",
        "upload_checkpoint_unavailable: write failed",
    ],
)
def test_deferred_upload_retry_reasons_are_recognized(reason):
    assert main.is_deferred_upload_retry({"failed_reason": reason}) is True
