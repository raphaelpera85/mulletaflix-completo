from __future__ import annotations

import pytest

from control_plane import ControlPlane, _normalize_permissions, _resolve_allowed, validate_ftp_security


@pytest.mark.parametrize(
    ("mode", "certfile", "keyfile", "required", "expected"),
    [
        ("ftp", None, None, False, False),
        ("ftps-explicit", "cert.pem", "key.pem", False, True),
        ("ftps-implicit", "cert.pem", "key.pem", True, True),
    ],
)
def test_validate_ftp_security_accepts_supported_modes(mode, certfile, keyfile, required, expected):
    assert validate_ftp_security(mode, certfile, keyfile, required) is expected


@pytest.mark.parametrize(
    ("mode", "certfile", "keyfile", "required"),
    [
        ("unknown", None, None, False),
        ("ftp", "cert.pem", None, False),
        ("ftps-explicit", None, None, False),
        ("ftp", None, None, True),
        ("ftp", "cert.pem", "key.pem", False),
    ],
)
def test_validate_ftp_security_rejects_inconsistent_tls_configuration(mode, certfile, keyfile, required):
    with pytest.raises(ValueError):
        validate_ftp_security(mode, certfile, keyfile, required)


def test_resolve_allowed_accepts_path_inside_root_and_rejects_escape(tmp_path):
    root = tmp_path / "library"
    nested = root / "Series" / "Episode.mkv"
    nested.parent.mkdir(parents=True)

    assert _resolve_allowed(str(nested), (root.resolve(),)) == nested.resolve()
    with pytest.raises(ValueError, match="outside configured roots"):
        _resolve_allowed(str(tmp_path / "outside"), (root.resolve(),))
    with pytest.raises(ValueError, match="outside configured roots"):
        _resolve_allowed(str(nested), ())


def test_normalize_permissions_makes_writable_paths_readable_and_deduplicates():
    assert _normalize_permissions(
        [
            {"path": "/Series", "writable": True},
            {"path": "/Films", "readable": True},
        ]
    ) == [
        {"path": "/Series", "readable": True, "writable": True},
        {"path": "/Films", "readable": True, "writable": False},
    ]
    with pytest.raises(ValueError, match="duplicate permission path"):
        _normalize_permissions([{"path": "/Series"}, {"path": "/Series"}])


@pytest.mark.parametrize("raw", ["elradfmwM", {}, "not-a-list", ["/Series"]])
def test_normalize_permissions_rejects_legacy_or_malformed_payloads(raw):
    with pytest.raises(ValueError):
        _normalize_permissions(raw)


@pytest.mark.parametrize("path", ["relative/path", "/Series/../Films"])
def test_normalize_permissions_rejects_non_normalized_paths(path):
    with pytest.raises(ValueError):
        _normalize_permissions([{"path": path}])


def test_control_plane_rejects_short_authentication_token():
    with pytest.raises(ValueError, match="at least 32 characters"):
        ControlPlane(
            token="short",
            mongo=object(),
            upload_queue=object(),
            drain_callback=lambda: None,
            status_provider=lambda: {},
        )
