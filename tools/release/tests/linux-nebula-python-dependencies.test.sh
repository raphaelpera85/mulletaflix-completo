#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
requirements_lock="$repo_root/MulletaFlix-master/Tools/NebulaPython/requirements.lock"
temporary_root="$(mktemp -d "${TMPDIR:-/tmp}/mulletaflix-nebula-python-deps.XXXXXXXX")"
cleanup() {
  case "$temporary_root" in
    "${TMPDIR:-/tmp}"/mulletaflix-nebula-python-deps.*) rm -rf -- "$temporary_root" ;;
    *) echo "Refusing to remove unexpected test path: $temporary_root" >&2; return 1 ;;
  esac
}
trap cleanup EXIT

python_bin="${PYTHON:-python3}"
"$python_bin" -m venv "$temporary_root/venv"
CC=false CXX=false "$temporary_root/venv/bin/python" -m pip install \
  --disable-pip-version-check \
  --require-hashes \
  -r "$requirements_lock"
"$temporary_root/venv/bin/python" -c 'import pyrogram, tgcrypto; print(f"Nebula Python dependencies ready: pyrogram={pyrogram.__version__}; tgcrypto={tgcrypto.__file__}")'
