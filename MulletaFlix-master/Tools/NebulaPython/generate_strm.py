"""Generate STRM files from completed Nebula MongoDB records."""

from __future__ import annotations

import os
import re
from pathlib import Path, PurePosixPath
from typing import Any

from pymongo import MongoClient

WINDOWS_INVALID_NAME = re.compile(r'[<>:"/\\|?*]')
PROTECTED_SIDECAR_EXTENSIONS = {
    ".nfo",
    ".jpg",
    ".jpeg",
    ".png",
    ".webp",
    ".avif",
    ".gif",
    ".bmp",
    ".tif",
    ".tiff",
    ".jpe",
    ".jif",
    ".jfif",
    ".jfi",
    ".tbn",
    ".xml",
    ".srt",
    ".sub",
    ".ass",
    ".ssa",
    ".vtt",
    ".smi",
    ".idx",
}
DIRECT_STREAM_EXTENSIONS = {".mp4", ".m4v", ".mov", ".webm"}


def safe_windows_name(value: Any) -> str:
    """Return a single Windows-safe path component."""
    cleaned = WINDOWS_INVALID_NAME.sub(" - ", str(value))
    cleaned = re.sub(r"\s+", " ", cleaned).strip(" .")
    return cleaned or "_"


def stream_endpoint(name: str) -> str:
    """Select direct playback for browser-compatible containers."""
    return "stream" if Path(name).suffix.lower() in DIRECT_STREAM_EXTENSIONS else "transcode"


def _path_parts(value: Any) -> list[str]:
    if value is None:
        return []
    normalized = str(value).replace("\\", "/")
    return [part for part in PurePosixPath(normalized).parts if part not in {"/", "."}]


def _build_directory_path_map(db: Any) -> dict[str, list[str]]:
    directories = {
        str(document["_id"]): document
        for document in db.files.find({"type": "dir"}, {"_id": 1, "name": 1, "parent": 1})
        if document.get("_id") is not None
    }
    resolved: dict[str, list[str]] = {}
    resolving: set[str] = set()

    def resolve(directory_id: Any) -> list[str]:
        key = str(directory_id)
        if key in resolved:
            return resolved[key]
        if key in resolving or key not in directories:
            return []

        resolving.add(key)
        document = directories[key]
        parent = document.get("parent")
        if parent is None:
            parent_parts: list[str] = []
        elif str(parent) in directories:
            parent_parts = resolve(parent)
        else:
            parent_parts = [safe_windows_name(part) for part in _path_parts(parent)]

        name = str(document.get("name", "")).strip()
        parts = [*parent_parts, safe_windows_name(name)] if name else parent_parts
        resolving.remove(key)
        resolved[key] = parts
        return parts

    for directory_id in directories:
        resolve(directory_id)
    return resolved


def generate_strm_files(
    *,
    mongo_url: str | None = None,
    database: str | None = None,
    output_root: str | os.PathLike[str] | None = None,
    stream_base_url: str | None = None,
    library_user: str | None = None,
    prune: bool = False,
) -> dict[str, Any]:
    """Write atomic STRM files for completed media without deleting existing files."""
    if prune:
        raise ValueError("STRM pruning is not supported by the control-plane generator")

    client = MongoClient(mongo_url or os.getenv("MONGODB", "mongodb://localhost:27017"))
    output = Path(output_root or os.getenv("STRM_OUTPUT_DIR", "strm_library")).resolve()
    base_url = (stream_base_url or os.getenv("STREAM_BASE_URL", "http://127.0.0.1:2122")).rstrip("/")
    user = (library_user or os.getenv("NEBULA_LIBRARY_USER", "raphael")).strip()
    generated: set[Path] = set()

    try:
        db = client[database or os.getenv("MONGO_DATABASE", "ftp")]
        directory_paths = _build_directory_path_map(db)
        completed_files = db.files.find(
            {"type": "file", "status": "completed", "parts.0": {"$exists": True}},
            {"_id": 1, "name": 1, "parent": 1},
        )

        for document in completed_files:
            name = str(document.get("name", "")).strip()
            file_id = document.get("_id")
            if not name or file_id is None or Path(name).suffix.lower() in PROTECTED_SIDECAR_EXTENSIONS:
                continue

            parent = document.get("parent")
            parent_parts = directory_paths.get(str(parent)) if parent is not None else None
            if parent_parts is None:
                parent_parts = [safe_windows_name(part) for part in _path_parts(parent)]
            if not parent_parts or parent_parts[0].casefold() != user.casefold():
                continue

            relative_parts = parent_parts[1:]
            target_directory = output.joinpath(*relative_parts)
            target_directory.mkdir(parents=True, exist_ok=True)
            target_name = f"{safe_windows_name(Path(name).stem)}.strm"
            target = target_directory / target_name
            temporary = target.with_name(f".{target.name}.{os.getpid()}.tmp")
            try:
                temporary.write_text(
                    f"{base_url}/{stream_endpoint(name)}?id={file_id}",
                    encoding="utf-8",
                )
                temporary.replace(target)
            finally:
                temporary.unlink(missing_ok=True)
            generated.add(target)
    finally:
        client.close()

    return {"generated": len(generated), "removed": 0, "output": str(output)}
