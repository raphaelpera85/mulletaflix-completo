#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
installer="$repo_root/tools/release/linux-install.sh"
test_root="$(mktemp -d)"
test_platform="$(uname -s)"
trap 'rm -rf -- "$test_root"' EXIT

function_source="$(sed -n '/^atomic_write_file() {/,/^}/p' "$installer")"
if [[ -z "$function_source" ]]; then
  echo 'Atomic writer function was not found in the Linux installer.' >&2
  exit 1
fi
# shellcheck disable=SC1090
source <(printf '%s\n' "$function_source")

target="$test_root/server.env"
printf 'OLD=contents\n' > "$target"
atomic_write_file "$target" 0640 <<'EOF'
NEW=contents
EOF
[[ "$(< "$target")" == 'NEW=contents' ]]
if [[ "$test_platform" != MINGW* && "$test_platform" != MSYS* && "$test_platform" != CYGWIN* ]]; then
  [[ "$(stat -c '%a' "$target")" == '640' ]]
fi
if compgen -G "$test_root/.server.env.*" >/dev/null; then
  echo 'Atomic writer left a temporary file after a successful replacement.' >&2
  exit 1
fi

protected_file="$test_root/protected.txt"
link_target="$test_root/duckdns.env"
printf 'DO_NOT_OVERWRITE\n' > "$protected_file"
ln -s "$protected_file" "$link_target"
atomic_write_file "$link_target" 0600 <<'EOF'
SAFE=contents
EOF
[[ ! -L "$link_target" ]]
[[ "$(< "$link_target")" == 'SAFE=contents' ]]
[[ "$(< "$protected_file")" == 'DO_NOT_OVERWRITE' ]]
if [[ "$test_platform" != MINGW* && "$test_platform" != MSYS* && "$test_platform" != CYGWIN* ]]; then
  [[ "$(stat -c '%a' "$link_target")" == '600' ]]
fi

printf 'ORIGINAL=contents\n' > "$target"
if atomic_write_file "$target" 0600 'mflx-test-user-must-not-exist-74219:mflx-test-group-must-not-exist-74219' 2>/dev/null <<'EOF'
SHOULD_NOT_REPLACE=contents
EOF
then
  echo 'Atomic writer accepted an invalid owner.' >&2
  exit 1
fi
[[ "$(< "$target")" == 'ORIGINAL=contents' ]]
if compgen -G "$test_root/.server.env.*" >/dev/null; then
  echo 'Atomic writer left a temporary file after ownership failure.' >&2
  exit 1
fi

if atomic_write_file "$target" invalid-mode 2>/dev/null <<'EOF'
SHOULD_NOT_REPLACE=contents
EOF
then
  echo 'Atomic writer accepted an invalid file mode.' >&2
  exit 1
fi
[[ "$(< "$target")" == 'ORIGINAL=contents' ]]
if compgen -G "$test_root/.server.env.*" >/dev/null; then
  echo 'Atomic writer left a temporary file after a failed replacement.' >&2
  exit 1
fi

echo 'Linux installer atomic writer replaces symlinks safely and preserves old data on write failure.'
