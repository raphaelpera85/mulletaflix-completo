from __future__ import annotations

import pytest

from ftp.range import parse_range


@pytest.mark.parametrize(
    ("header", "size", "expected"),
    [
        (None, 10, (0, 9, 200)),
        ("", 10, (0, 9, 200)),
        ("items=0-1", 10, (0, 9, 200)),
        ("bytes=0-0", 10, (0, 0, 206)),
        ("bytes=2-5", 10, (2, 5, 206)),
        ("bytes=7-", 10, (7, 9, 206)),
        ("bytes=-3", 10, (7, 9, 206)),
        ("bytes=-30", 10, (0, 9, 206)),
        ("bytes=2-30", 10, (2, 9, 206)),
        ("bytes=4-2", 10, (0, 9, 416)),
        ("bytes=10-", 10, (0, 9, 416)),
        ("bytes=0-", 0, (0, -1, 416)),
        ("bytes=0-1,4-5", 10, (0, 9, 416)),
        ("bytes=invalid", 10, (0, 9, 200)),
        ("bytes=-0", 10, (0, 9, 200)),
    ],
)
def test_parse_range_contract(header: str | None, size: int, expected: tuple[int, int, int]) -> None:
    assert parse_range(header, size) == expected

