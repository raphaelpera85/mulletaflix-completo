"""Durable, credential-free checkpoints for Telegram upload parts."""

from __future__ import annotations

import json
import hashlib
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


def compute_part_sha256(data: bytes) -> str:
    """Return SHA-256 for the exact bytes passed to Telegram."""
    return hashlib.sha256(data).hexdigest()


def compute_file_part_sha256(
    path: str | os.PathLike[str], part_number: int, source_size: int, chunk_size: int
) -> str:
    """Hash a logical source part in bounded memory for resume validation."""
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
                raise OSError("A origem mudou ou foi truncada durante a validação de uma parte Nebula.")
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

    Checkpoints contain only the opaque upload UUID and Telegram part metadata;
    source paths and bot tokens are intentionally excluded.
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

            if (
                not isinstance(document, dict)
                or document.get("file_uuid") != str(uuid.UUID(file_uuid))
            ):
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
            if not isinstance(raw_parts, list) or not isinstance(raw_pending, list):
                raise ValueError("O checkpoint local do upload Nebula está corrompido.")

            parts: dict[int, dict[str, Any]] = {}
            for part in raw_parts:
                part_number = self._validate_part(
                    part, file_uuid, source_size, total_parts, chunk_size, allow_unverified=True
                )
                if part_number in parts:
                    raise ValueError("O checkpoint local contém partes duplicadas.")
                parts[part_number] = part

            pending: set[int] = set()
            pending_sha256: dict[int, str] = {}
            requires_new_generation = any(
                not isinstance(part.get("source_sha256"), str)
                or not _SHA256_PATTERN.fullmatch(part["source_sha256"])
                for part in parts.values()
            )
            raw_pending_hashes = document.get("pending_part_sha256", {})
            if not isinstance(raw_pending_hashes, dict):
                raise ValueError("O checkpoint local contém hashes de intenção inválidos.")
            for part_number in raw_pending:
                if isinstance(part_number, bool) or not isinstance(part_number, int):
                    raise ValueError("O checkpoint local contém uma intenção de parte inválida.")
                if part_number < 0 or part_number >= total_parts:
                    raise ValueError("O checkpoint local contém uma intenção de parte fora dos limites.")
                if part_number not in parts:
                    pending.add(part_number)
                    digest = raw_pending_hashes.get(str(part_number))
                    if isinstance(digest, str) and _SHA256_PATTERN.fullmatch(digest):
                        pending_sha256[part_number] = digest.lower()
                    else:
                        requires_new_generation = True

            return UploadCheckpointSnapshot(
                tuple(parts[number] for number in sorted(parts)),
                frozenset(pending),
                pending_sha256,
                requires_new_generation,
            )

    def mark_pending(
        self,
        file_uuid: str,
        source_size: int,
        source_mtime_ns: int,
        total_parts: int,
        chunk_size: int,
        part_number: int,
        source_sha256: str,
    ) -> None:
        if part_number < 0 or part_number >= total_parts or not _SHA256_PATTERN.fullmatch(source_sha256):
            raise ValueError("Não é possível registrar intenção de parte fora dos limites do upload.")
        with self._lock:
            snapshot = self.load(file_uuid, source_size, source_mtime_ns, total_parts, chunk_size)
            if any(part["part_id"] == part_number for part in snapshot.parts):
                return
            self._write(
                file_uuid,
                source_size,
                source_mtime_ns,
                total_parts,
                chunk_size,
                snapshot.parts,
                set(snapshot.pending_parts) | {part_number},
                {**(snapshot.pending_sha256 or {}), part_number: source_sha256.lower()},
            )

    def save_part(
        self,
        file_uuid: str,
        source_size: int,
        source_mtime_ns: int,
        total_parts: int,
        chunk_size: int,
        part: dict[str, Any],
    ) -> None:
        part_number = self._validate_part(part, file_uuid, source_size, total_parts, chunk_size)
        with self._lock:
            snapshot = self.load(file_uuid, source_size, source_mtime_ns, total_parts, chunk_size)
            parts = {item["part_id"]: item for item in snapshot.parts}
            parts[part_number] = part
            pending = set(snapshot.pending_parts)
            pending.discard(part_number)
            self._write(
                file_uuid,
                source_size,
                source_mtime_ns,
                total_parts,
                chunk_size,
                tuple(parts[number] for number in sorted(parts)),
                pending,
                {number: digest for number, digest in (snapshot.pending_sha256 or {}).items() if number != part_number},
            )

    def delete(self, file_uuid: str) -> None:
        path = self._get_path(file_uuid)
        with self._lock:
            try:
                path.unlink()
            except FileNotFoundError:
                pass

    def _write(
        self,
        file_uuid: str,
        source_size: int,
        source_mtime_ns: int,
        total_parts: int,
        chunk_size: int,
        parts: tuple[dict[str, Any], ...],
        pending_parts: set[int],
        pending_sha256: dict[int, str],
    ) -> None:
        file_uuid = str(uuid.UUID(file_uuid))
        document = {
            "format_version": CURRENT_FORMAT_VERSION,
            "file_uuid": file_uuid,
            "source_size": source_size,
            "source_mtime_ns": source_mtime_ns,
            "total_parts": total_parts,
            "chunk_size": chunk_size,
            "parts": list(parts),
            "pending_part_numbers": sorted(pending_parts),
            "pending_part_sha256": {str(number): digest for number, digest in sorted(pending_sha256.items())},
        }
        self._directory.mkdir(parents=True, exist_ok=True)
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f"{uuid.UUID(file_uuid).hex}.",
            suffix=".tmp",
            dir=self._directory,
        )
        temporary_path = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(document, stream, ensure_ascii=False, separators=(",", ":"))
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_path, self._get_path(file_uuid))
        finally:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass

    @staticmethod
    def _validate_part(
        part: Any,
        file_uuid: str,
        source_size: int,
        total_parts: int,
        chunk_size: int,
        allow_unverified: bool = False,
    ) -> int:
        if not isinstance(part, dict):
            raise ValueError("O checkpoint local contém uma parte inválida.")
        part_number = part.get("part_id")
        if isinstance(part_number, bool) or not isinstance(part_number, int):
            raise ValueError("O checkpoint local contém uma parte inválida.")
        if part_number < 0 or part_number >= total_parts:
            raise ValueError("O checkpoint local contém uma parte fora dos limites.")
        expected_size = min(chunk_size, source_size - part_number * chunk_size)
        required_size = part.get("file_size")
        message_id = part.get("tg_message")
        bot_index = part.get("bot_index", 0)
        if (
            not isinstance(part.get("tg_file"), str)
            or not part["tg_file"]
            or isinstance(message_id, bool)
            or not isinstance(message_id, int)
            or message_id <= 0
            or isinstance(required_size, bool)
            or not isinstance(required_size, int)
            or required_size != expected_size
            or part.get("chunk_name") != f"{file_uuid}.part_{part_number:03d}"
            or (
                not allow_unverified
                and (
                    not isinstance(part.get("source_sha256"), str)
                    or not _SHA256_PATTERN.fullmatch(part["source_sha256"])
                )
            )
            or part.get("byte_start") != part_number * chunk_size
            or part.get("byte_end") != part_number * chunk_size + expected_size - 1
            or isinstance(bot_index, bool)
            or not isinstance(bot_index, int)
            or bot_index < 0
        ):
            raise ValueError("O checkpoint local contém uma parte incompleta ou inválida.")
        return part_number

    def _get_path(self, file_uuid: str) -> Path:
        try:
            parsed = uuid.UUID(file_uuid)
        except (ValueError, AttributeError, TypeError) as exc:
            raise ValueError("O identificador do upload Nebula não é um UUID válido.") from exc
        return self._directory / f"{parsed.hex}.json"
