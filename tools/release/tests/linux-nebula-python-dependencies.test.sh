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

if [[ $# -gt 0 ]]; then
  package_archive="$1"
  test -f "$package_archive"
  package_root="$temporary_root/package"
  mkdir -p "$package_root"

  while IFS= read -r member; do
    case "$member" in
      /*|../*|*/../*|*/..) echo "Unsafe package path: $member" >&2; exit 1 ;;
    esac
  done < <(tar -tzf "$package_archive")
  tar -xzf "$package_archive" -C "$package_root"

  runtime_dirs="$(find "$package_root" -type d -path '*/server/Tools/NebulaPython' -print)"
  test "$(printf '%s\n' "$runtime_dirs" | wc -l)" -eq 1
  runtime_dir="$runtime_dirs"
  cmp "$requirements_lock" "$runtime_dir/requirements.lock"

  (
    cd "$runtime_dir"
    export PYTHONDONTWRITEBYTECODE=1
    export NEBULA_STREAM_SERVICE=1
    export NEBULA_ENV_FILE="$temporary_root/nonexistent.env"
    timeout 30s "$temporary_root/venv/bin/python" -c 'import main; print("Packaged Nebula main import passed")'
    timeout 30s "$temporary_root/venv/bin/python" tools/strm_downloader.py --help >/dev/null
    echo "Packaged Nebula downloader CLI smoke passed"
  )
fi
