#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -eq 0 ]]; then
  echo 'Preflight test must run unprivileged to prevent accidental installation.' >&2
  exit 1
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf -- "$test_root"' EXIT
package_root="$test_root/package"
mkdir -p "$package_root/server"
cp -- "$repo_root/tools/release/linux-install.sh" "$package_root/install.sh"
: > "$package_root/server/MulletaFlix"

assert_preflight_error() {
  local expected="$1"
  shift
  if env -u MULLETAFLIX_PUBLIC_HOST -u MULLETAFLIX_ACME_EMAIL -u MULLETAFLIX_DUCKDNS_SUBDOMAIN \
    -u MULLETAFLIX_SKIP_HTTPS bash "$package_root/install.sh" "$@" > "$test_root/stdout" 2> "$test_root/stderr"; then
    echo "Installer unexpectedly accepted invalid preflight input: $expected" >&2
    exit 1
  fi
  if ! grep -Fq "$expected" "$test_root/stderr"; then
    echo "Expected preflight error was not reported before privilege checks: $expected" >&2
    cat "$test_root/stderr" >&2
    exit 1
  fi
  if grep -Fq 'Run as root:' "$test_root/stderr"; then
    echo "Privilege gate ran before input validation: $expected" >&2
    exit 1
  fi
}

assert_preflight_error 'HTTPS requested but --https-email' --https-host media.example.test
assert_preflight_error 'Invalid ACME notification email.' --https-host media.example.test --https-email not-an-email
assert_preflight_error 'Invalid public hostname.' --https-host 'bad host' --https-email user@example.test
assert_preflight_error 'Invalid DuckDNS subdomain.' --duckdns-subdomain 'Bad_Subdomain' --https-email user@example.test

if env -u MULLETAFLIX_PUBLIC_HOST -u MULLETAFLIX_ACME_EMAIL -u MULLETAFLIX_DUCKDNS_SUBDOMAIN \
  -u MULLETAFLIX_SKIP_HTTPS MULLETAFLIX_PUBLIC_HOST=media.example.test MULLETAFLIX_ACME_EMAIL=user@example.test \
  bash "$package_root/install.sh" > "$test_root/valid.stdout" 2> "$test_root/valid.stderr"; then
  echo 'Valid HTTPS inputs unexpectedly passed the root-only installation gate.' >&2
  cat "$test_root/valid.stderr" >&2
  exit 1
fi
if ! grep -Fq 'Run as root:' "$test_root/valid.stderr"; then
  echo 'Valid HTTPS inputs did not pass preflight to the root-only installation gate.' >&2
  cat "$test_root/valid.stderr" >&2
  exit 1
fi

echo 'Linux installer rejects invalid HTTPS/DuckDNS input before privilege checks or installation.'
