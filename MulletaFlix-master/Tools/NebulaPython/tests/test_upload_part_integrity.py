from __future__ import annotations

import hashlib
import os
from pathlib import Path

from upload_checkpoint import compute_file_part_sha256, compute_part_sha256


def test_part_sha256_matches_exact_uploaded_bytes() -> None:
    payload = b"same-length payload"
    assert compute_part_sha256(payload) == hashlib.sha256(payload).hexdigest()


def test_file_part_sha256_detects_changed_bytes_with_same_size_and_mtime(tmp_path: Path) -> None:
    source = tmp_path / "media.bin"
    source.write_bytes(b"abcdefgh")
    original_stat = source.stat()
    original_digest = compute_file_part_sha256(source, 0, 8, 4)

    source.write_bytes(b"ABCDEFGH")
    os.utime(source, ns=(original_stat.st_atime_ns, original_stat.st_mtime_ns))

    assert compute_file_part_sha256(source, 0, 8, 4) != original_digest


def test_file_part_sha256_respects_part_offset(tmp_path: Path) -> None:
    source = tmp_path / "media.bin"
    source.write_bytes(b"abcdefghij")

    assert compute_file_part_sha256(source, 1, 10, 4) == hashlib.sha256(b"efgh").hexdigest()
    assert compute_file_part_sha256(source, 2, 10, 4) == hashlib.sha256(b"ij").hexdigest()
