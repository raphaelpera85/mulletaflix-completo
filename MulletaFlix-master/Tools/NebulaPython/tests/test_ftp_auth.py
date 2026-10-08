from __future__ import annotations

import importlib
import logging
import sys

import pytest

from ftp import auth


def test_hash_password_rejects_empty_input():
    with pytest.raises(ValueError, match="empty password"):
        auth.hash_password("")


def test_hash_password_and_verify_support_utf8(monkeypatch):
    monkeypatch.setattr(auth, "_BCRYPT_ROUNDS", 4)
    stored = auth.hash_password("senha-ç-🔐")

    assert auth.is_hashed(stored)
    assert auth.verify_password("senha-ç-🔐", stored)
    assert not auth.verify_password("senha-errada", stored)


@pytest.mark.parametrize("stored", [None, "", "$2b$malformed"])
def test_verify_password_fails_closed_for_missing_or_malformed_hash(stored):
    assert not auth.verify_password("password", stored)


def test_verify_password_supports_legacy_plaintext_with_exact_comparison():
    assert auth.verify_password("legacy", "legacy")
    assert not auth.verify_password("Legacy", "legacy")
    assert not auth.verify_password("", "")


def test_bcrypt_import_failure_requires_hashing_dependency_and_fails_closed(monkeypatch, caplog):
    try:
        with monkeypatch.context() as patch:
            patch.setitem(sys.modules, "bcrypt", None)
            importlib.reload(auth)

            assert not auth._HAVE_BCRYPT
            with pytest.raises(RuntimeError, match="bcrypt is required"):
                auth.hash_password("secret")

            with caplog.at_level(logging.ERROR, logger="NebulaFTP"):
                assert not auth.verify_password("secret", "$2b$malformed")
            assert "bcrypt missing — cannot verify hashed password" in caplog.text
    finally:
        importlib.reload(auth)
