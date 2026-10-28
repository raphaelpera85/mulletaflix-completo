#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
temporary_root="$(mktemp -d "${TMPDIR:-/tmp}/mulletaflix-nebula-package-test.XXXXXXXX")"
cleanup() {
  case "$temporary_root" in
    "${TMPDIR:-/tmp}"/mulletaflix-nebula-package-test.*) rm -rf -- "$temporary_root" ;;
    *) echo "Refusing to remove unexpected test path: $temporary_root" >&2; return 1 ;;
  esac
}
trap cleanup EXIT

source_dir="$repo_root/MulletaFlix-master/Tools/NebulaPython"
destination_dir="$temporary_root/server/Tools/NebulaPython"
bash "$repo_root/tools/release/copy-nebula-python-runtime.sh" "$source_dir" "$destination_dir"

for required_file in \
  main.py stream_service.py control_plane.py generate_strm.py upload_checkpoint.py requirements.in requirements.txt requirements.lock \
  ftp/__init__.py tools/__init__.py tools/strm_downloader.py tools/mongo_admission.py; do
  if [[ ! -f "$destination_dir/$required_file" ]]; then
    echo "Required packaged Nebula runtime file missing: $required_file" >&2
    exit 1
  fi
done
cmp "$source_dir/tools/strm_downloader.py" "$destination_dir/tools/strm_downloader.py"
cmp "$source_dir/upload_checkpoint.py" "$destination_dir/upload_checkpoint.py"
if find "$destination_dir" -type d \( -name tests -o -name __pycache__ -o -name .pytest_cache \) -print -quit | grep -q .; then
  echo "Test or cache directory leaked into the packaged Nebula runtime." >&2
  exit 1
fi

echo "Linux package includes the current Nebula Python runtime and STRM downloader."
