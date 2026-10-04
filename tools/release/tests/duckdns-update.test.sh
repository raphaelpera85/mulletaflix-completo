#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
script="$repo_root/tools/release/duckdns-update.sh"
test_root="$(mktemp -d)"
trap 'rm -rf -- "$test_root"' EXIT
mkdir -p "$test_root/bin"
mkdir -p "$test_root/tmp"

cat > "$test_root/bin/curl" <<'CURL_STUB'
#!/usr/bin/env bash
set -euo pipefail
config_path=""
for ((index = 1; index <= $#; index++)); do
  argument="${!index}"
  if [[ "$argument" == *"duckdns-test-secret"* ]]; then
    echo "secret leaked in curl arguments" >&2
    exit 91
  fi
  if [[ "$argument" == "--config" ]]; then
    next_index=$((index + 1))
    config_path="${!next_index}"
  fi
done
if [[ -z "$config_path" || ! -f "$config_path" ]]; then
  echo "curl config file not provided" >&2
  exit 92
fi
if [[ "$(stat -c '%a' "$config_path")" != "600" ]]; then
  echo "curl config file permissions are not private" >&2
  exit 93
fi
cp -- "$config_path" "$DUCKDNS_TEST_CAPTURE"
printf '%s' "${DUCKDNS_TEST_RESPONSE:-OK}"
CURL_STUB
chmod 0700 "$test_root/bin/curl"

config_file="$test_root/duckdns.env"
capture_file="$test_root/request.conf"
marker_file="$test_root/config-was-executed"
token='duckdns-test-secret+& /?'
printf 'DUCKDNS_SUBDOMAIN=test-domain\nDUCKDNS_TOKEN=%s\n' "$token" > "$config_file"
chmod 0600 "$config_file"

output="$(PATH="$test_root/bin:$PATH" TMPDIR="$test_root/tmp" DUCKDNS_TEST_CAPTURE="$capture_file" \
  "$script" "$config_file")"
[[ "$output" == "DuckDNS updated: test-domain.duckdns.org" ]]
grep -Fq 'domains=test-domain&token=duckdns-test-secret%2B%26%20%2F%3F&ip=' "$capture_file"
if [[ -e "$capture_file" ]] && grep -Fq "$token" "$capture_file"; then
  echo "token was not URL-encoded in DuckDNS request configuration" >&2
  exit 1
fi
if compgen -G "$test_root/tmp/mulletaflix-duckdns.*" >/dev/null; then
  echo "temporary DuckDNS request configuration was not removed" >&2
  exit 1
fi

if PATH="$test_root/bin:$PATH" TMPDIR="$test_root/tmp" DUCKDNS_TEST_CAPTURE="$capture_file" DUCKDNS_TEST_RESPONSE=KO \
  "$script" "$config_file" > "$test_root/failure.out" 2> "$test_root/failure.err"; then
  echo "DuckDNS failure response was accepted" >&2
  exit 1
fi
grep -Fq 'DuckDNS update failed.' "$test_root/failure.err"
if grep -Fq "$token" "$test_root/failure.err"; then
  echo "token leaked to failure output" >&2
  exit 1
fi

printf 'DUCKDNS_SUBDOMAIN=test-domain\nDUCKDNS_TOKEN=%s\n$(touch %s)\n' \
  "$token" "$marker_file" > "$test_root/untrusted.env"
chmod 0600 "$test_root/untrusted.env"
if PATH="$test_root/bin:$PATH" TMPDIR="$test_root/tmp" DUCKDNS_TEST_CAPTURE="$capture_file" \
  "$script" "$test_root/untrusted.env" > "$test_root/untrusted.out" 2> "$test_root/untrusted.err"; then
  echo "unknown DuckDNS config setting was accepted" >&2
  exit 1
fi
if [[ -e "$marker_file" ]]; then
  echo "DuckDNS config file was executed as shell code" >&2
  exit 1
fi

echo "DuckDNS updater security tests passed."
