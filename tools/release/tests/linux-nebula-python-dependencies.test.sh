#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
requirements="$repo_root/MulletaFlix-master/Tools/NebulaPython/requirements.txt"
temporary_root="$(mktemp -d "${TMPDIR:-/tmp}/mulletaflix-nebula-python-deps.XXXXXXXX")"
cleanup() {
  case "$temporary_root" in
    "${TMPDIR:-/tmp}"/mulletaflix-nebula-python-deps.*) rm -rf -- "$temporary_root" ;;
    *) echo "Refusing to remove unexpected test path: $temporary_root" >&2; return 1 ;;
  esac
}
trap cleanup EXIT

python_bin="${PYTHON:-python3}"
mkdir -p "$temporary_root/wheels"
"$python_bin" -m venv "$temporary_root/venv"
"$temporary_root/venv/bin/python" -m pip download \
  --disable-pip-version-check \
  --only-binary=:all: \
  --no-deps \
  --dest "$temporary_root/wheels" \
  'tgcrypto-pyrofork>=1.2.8,<1.3'
"$temporary_root/venv/bin/python" -m pip install \
  --disable-pip-version-check \
  --no-index \
  --find-links "$temporary_root/wheels" \
  'tgcrypto-pyrofork>=1.2.8,<1.3'
CC=false CXX=false "$temporary_root/venv/bin/python" -m pip install \
  --disable-pip-version-check \
  -r "$requirements"
"$temporary_root/venv/bin/python" -c 'import pyrogram, tgcrypto; print(f"Nebula Python dependencies ready: pyrogram={pyrogram.__version__}; tgcrypto={tgcrypto.__file__}")'
