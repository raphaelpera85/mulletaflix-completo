"""Durable, credential-free checkpoints for Telegram upload parts."""

from __future__ import annotations

import hashlib
import json
import os
import re
import tempfile
import threading
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

CURRENT_FORMAT_VERSION = 2
_SHA256_PATTERN = re.compile(r"^[0-9a-fA-F]{64}$")


def _sync_directory(directory: Path) -> None:
    """Persist directory-entry changes after atomic checkpoint replacement."""
    descriptor = os.open(directory, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def _replace_checkpoint_file(source: Path, destination: Path) -> None:
    """Replace the checkpoint and make its directory entry durable."""
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        move_file = kernel32.MoveFileExW
        move_file.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.DWORD]
        move_file.restype = wintypes.BOOL
        if not move_file(str(source), str(destination), 0x1 | 0x8):
            raise ctypes.WinError(ctypes.get_last_error())
        return

    os.replace(source, destination)
    _sync_directory(destination.parent)


def compute_part_sha256(data: bytes) -> str:
    """Hash the exact bytes passed to Telegram."""
    return hashlib.sha256(data).hexdigest()


def compute_file_part_sha256(
    path: str | os.PathLike[str], part_number: int, source_size: int, chunk_size: int
) -> str:
    """Hash one source part using bounded memory."""
    if part_number < 0 or source_size <= 0 or chunk_size <= 0:
        raise ValueError("Índice/tamanho inválido ao validar parte Nebula.")
    offset = part_number * chunk_size
    if offset >= source_size:
        raise ValueError("A parte Nebula excede o tamanho da origem.")
    remaining = min(chunk_size, source_size - offset)
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        stream.seek(offset)
        while remaining:
            block = stream.read(min(128 * 1024, remaining))
            if not block:
                raise OSError("A origem mudou ou foi truncada durante a validação.")
            digest.update(block)
            remaining -= len(block)
    return digest.hexdigest()


@dataclass(frozen=True)
class UploadCheckpointSnapshot:
    parts: tuple[dict[str, Any], ...] = ()
    pending_parts: frozenset[int] = frozenset()
    pending_sha256: dict[int, str] | None = None
    requires_new_generation: bool = False


class UploadCheckpointJournal:
    """Atomically persist Telegram acknowledgements before Mongo progress writes.

    The journal contains no source path or bot credentials.
    """

    def __init__(self, directory: str | os.PathLike[str]):
        self._directory = Path(directory).expanduser()
        self._lock = threading.RLock()

    def load(
        self,
        file_uuid: str,
        source_size: int,
        source_mtime_ns: int,
        total_parts: int,
        chunk_size: int,
    ) -> UploadCheckpointSnapshot:
        path = self._get_path(file_uuid)
        with self._lock:
            if not path.exists():
                return UploadCheckpointSnapshot()
            try:
                with path.open("r", encoding="utf-8") as stream:
                    document = json.load(stream)
            except (OSError, UnicodeError, json.JSONDecodeError) as exc:
                raise ValueError("O checkpoint local do upload Nebula está corrompido.") from exc
            if not isinstance(document, dict) or document.get("file_uuid") != str(uuid.UUID(file_uuid)):
                raise ValueError("O checkpoint local não corresponde ao UUID do arquivo atual.")
            if (
                document.get("format_version") == 1
                or document.get("source_size") != source_size
                or document.get("source_mtime_ns") != source_mtime_ns
                or document.get("total_parts") != total_parts
                or document.get("chunk_size") != chunk_size
            ):
                return UploadCheckpointSnapshot(requires_new_generation=True)
            if document.get("format_version") != CURRENT_FORMAT_VERSION:
                raise ValueError("A versão do checkpoint local do upload Nebula não é suportada.")

            raw_parts = document.get("parts", [])
            raw_pending = document.get("pending_part_numbers", [])
            raw_pending_hashes = document.get("pending_part_sha256", {})
            if not isinstance(raw_parts, list) or not isinstance(raw_pending, list) or not isinstance(raw_pending_hashes, dict):
                raise ValueError("O checkpoint local do upload Nebula está corrompido.")

            parts: dict[int, dict[str, Any]] = {}
            for part in raw_parts:
                number = self._validate_part(part, file_uuid, source_size, total_parts, chunk_size, allow_unverified=True)
                if number in parts:
                    raise ValueError("O checkpoint local contém partes duplicadas.")
                parts[number] = part

            pending: set[int] = set()
            pending_sha256: dict[int, str] = {}
            requires_new_generation = any(
                not isinstance(part.get("source_sha256"), str)
                or not _SHA256_PATTERN.fullmatch(part["source_sha256"])
                for part in parts.values()
            )
            for number in raw_pending:
                if isinstance(number, bool) or not isinstance(number, int) or not 0 <= number < total_parts:
                    raise ValueError("O checkpoint local contém uma intenção de parte inválida.")
                if number in parts:
                    continue
                pending.add(number)
                digest = raw_pending_hashes.get(str(number))
                if isinstance(digest, str) and _SHA256_PATTERN.fullmatch(digest):
                    pending_sha256[number] = digest.lower()
                else:
                    requires_new_generation = True
            return UploadCheckpointSnapshot(
                tuple(parts[number] for number in sorted(parts)),
                frozenset(pending),
                pending_sha256,
                requires_new_generation,
            )

    def mark_pending(
        self, file_uuid: str, source_size: int, source_mtime_ns: int,
        total_parts: int, chunk_size: int, part_number: int, source_sha256: str,
    ) -> None:
        if part_number < 0 or part_number >= total_parts or not _SHA256_PATTERN.fullmatch(source_sha256):
            raise ValueError("Não é possível registrar intenção de parte fora dos limites do upload.")
        with self._lock:
            snapshot = self.load(file_uuid, source_size, source_mtime_ns, total_parts, chunk_size)
            if any(part["part_id"] == part_number for part in snapshot.parts):
                return
            pending_hashes = dict(snapshot.pending_sha256 or {})
            pending_hashes[part_number] = source_sha256.lower()
            self._write(
                file_uuid, source_size, source_mtime_ns, total_parts, chunk_size,
                snapshot.parts, set(snapshot.pending_parts) | {part_number}, pending_hashes,
            )

    def save_part(
        self, file_uuid: str, source_size: int, source_mtime_ns: int,
        total_parts: int, chunk_size: int, part: dict[str, Any],
    ) -> None:
        number = self._validate_part(part, file_uuid, source_size, total_parts, chunk_size)
        with self._lock:
            snapshot = self.load(file_uuid, source_size, source_mtime_ns, total_parts, chunk_size)
            parts = {item["part_id"]: item for item in snapshot.parts}
            parts[number] = part
            pending = set(snapshot.pending_parts)
            pending.discard(number)
            pending_hashes = {
                key: value for key, value in (snapshot.pending_sha256 or {}).items() if key != number
            }
            self._write(
                file_uuid, source_size, source_mtime_ns, total_parts, chunk_size,
                tuple(parts[index] for index in sorted(parts)), pending, pending_hashes,
            )

    def delete(self, file_uuid: str) -> None:
        with self._lock:
            try:
                self._get_path(file_uuid).unlink()
            except FileNotFoundError:
                pass

    def _write(
        self, file_uuid: str, source_size: int, source_mtime_ns: int,
        total_parts: int, chunk_size: int, parts: tuple[dict[str, Any], ...],
        pending_parts: set[int], pending_sha256: dict[int, str],
    ) -> None:
        normalized_uuid = str(uuid.UUID(file_uuid))
        self._directory.mkdir(parents=True, exist_ok=True)
        document = {
            "format_version": CURRENT_FORMAT_VERSION,
            "file_uuid": normalized_uuid,
            "source_size": source_size,
            "source_mtime_ns": source_mtime_ns,
            "total_parts": total_parts,
            "chunk_size": chunk_size,
            "parts": list(parts),
            "pending_part_numbers": sorted(pending_parts),
            "pending_part_sha256": {str(key): value for key, value in sorted(pending_sha256.items())},
        }
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f"{uuid.UUID(normalized_uuid).hex}.", suffix=".tmp", dir=self._directory,
        )
        temporary_path = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(document, stream, ensure_ascii=False, separators=(",", ":"))
                stream.flush()
                os.fsync(stream.fileno())
            _replace_checkpoint_file(temporary_path, self._get_path(normalized_uuid))
        finally:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass

    @staticmethod
    def _validate_part(
        part: Any, file_uuid: str, source_size: int, total_parts: int,
        chunk_size: int, allow_unverified: bool = False,
    ) -> int:
        if not isinstance(part, dict):
            raise ValueError("O checkpoint local contém uma parte inválida.")
        number = part.get("part_id")
        if isinstance(number, bool) or not isinstance(number, int) or not 0 <= number < total_parts:
            raise ValueError("O checkpoint local contém uma parte fora dos limites.")
        expected_size = min(chunk_size, source_size - number * chunk_size)
        message_id = part.get("tg_message")
        bot_index = part.get("bot_index", 0)
        digest = part.get("source_sha256")
        if (
            not isinstance(part.get("tg_file"), str) or not part["tg_file"]
            or isinstance(message_id, bool) or not isinstance(message_id, int) or message_id <= 0
            or isinstance(part.get("file_size"), bool) or part.get("file_size") != expected_size
            or part.get("chunk_name") != f"{uuid.UUID(file_uuid)}.part_{number:03d}"
            or (not allow_unverified and (not isinstance(digest, str) or not _SHA256_PATTERN.fullmatch(digest)))
            or part.get("byte_start") != number * chunk_size
            or part.get("byte_end") != number * chunk_size + expected_size - 1
            or isinstance(bot_index, bool) or not isinstance(bot_index, int) or bot_index < 0
        ):
            raise ValueError("O checkpoint local contém uma parte incompleta ou inválida.")
        return number

    def _get_path(self, file_uuid: str) -> Path:
        try:
            parsed = uuid.UUID(file_uuid)
        except (ValueError, AttributeError, TypeError) as exc:
            raise ValueError("O identificador do upload Nebula não é um UUID válido.") from exc
        return self._directory / f"{parsed.hex}.json"
