"""Canonical spelling for Nebula queue paths, matching the C# implementation."""

import os


def queue_identity(path: str) -> str:
    """Return the normalized lexical path key used to deduplicate queue entries."""
    identity = os.path.normcase(os.path.abspath(path)).rstrip("\\/")
    return identity.upper() if os.name == "nt" else identity
