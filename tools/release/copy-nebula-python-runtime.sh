#!/usr/bin/env bash
set -euo pipefail

source_dir="${1:?usage: copy-nebula-python-runtime.sh <source-dir> <destination-dir>}"
destination_dir="${2:?usage: copy-nebula-python-runtime.sh <source-dir> <destination-dir>}"

for required_file in main.py stream_service.py control_plane.py generate_strm.py requirements.in requirements.txt requirements.lock; do
  if [[ ! -f "$source_dir/$required_file" ]]; then
    echo "Required Nebula Python runtime file missing: $source_dir/$required_file" >&2
    exit 1
  fi
done
for required_file in __init__.py strm_downloader.py mongo_admission.py; do
  if [[ ! -f "$source_dir/tools/$required_file" ]]; then
    echo "Required Nebula Python tool missing: $source_dir/tools/$required_file" >&2
    exit 1
  fi
done

mkdir -p "$destination_dir/ftp" "$destination_dir/tools"
for runtime_file in main.py stream_service.py control_plane.py generate_strm.py requirements.in requirements.txt requirements.lock; do
  cp "$source_dir/$runtime_file" "$destination_dir/$runtime_file"
done
find "$source_dir/ftp" -maxdepth 1 -type f -name '*.py' -exec cp {} "$destination_dir/ftp/" \;
find "$source_dir/tools" -maxdepth 1 -type f -name '*.py' -exec cp {} "$destination_dir/tools/" \;
