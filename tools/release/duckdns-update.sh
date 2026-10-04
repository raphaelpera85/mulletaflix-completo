#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-/etc/mulletaflix/duckdns.env}"
if [[ ! -r "$env_file" ]]; then
  echo "DuckDNS configuration not found: $env_file" >&2
  exit 1
fi
duckdns_subdomain=""
duckdns_token=""
while IFS='=' read -r key value || [[ -n "${key:-}${value:-}" ]]; do
  case "$key" in
    DUCKDNS_SUBDOMAIN)
      if [[ -n "$duckdns_subdomain" ]]; then
        echo "Duplicate DuckDNS subdomain setting." >&2
        exit 1
      fi
      duckdns_subdomain="$value"
      ;;
    DUCKDNS_TOKEN)
      if [[ -n "$duckdns_token" ]]; then
        echo "Duplicate DuckDNS token setting." >&2
        exit 1
      fi
      duckdns_token="$value"
      ;;
    *)
      echo "Unknown DuckDNS configuration setting." >&2
      exit 1
      ;;
  esac
done < "$env_file"
: "${duckdns_subdomain:?DUCKDNS_SUBDOMAIN is required}"
: "${duckdns_token:?DUCKDNS_TOKEN is required}"
if [[ ! "$duckdns_subdomain" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$ ]]; then
  echo "Invalid DuckDNS subdomain." >&2
  exit 1
fi
if [[ "$duckdns_token" == *$'\n'* || "$duckdns_token" == *$'\r'* ]]; then
  echo "Invalid DuckDNS token format." >&2
  exit 1
fi

urlencode() {
  local input="$1" output="" char code index LC_ALL=C
  for ((index = 0; index < ${#input}; index++)); do
    char="${input:index:1}"
    case "$char" in
      [a-zA-Z0-9.~_-]) output+="$char" ;;
      *)
        printf -v code '%d' "'${char}"
        printf -v char '%%%02X' "$code"
        output+="$char"
        ;;
    esac
  done
  printf '%s' "$output"
}

umask 077
request_config="$(mktemp "${TMPDIR:-/tmp}/mulletaflix-duckdns.XXXXXX")"
trap 'rm -f -- "$request_config"; unset duckdns_token' EXIT
printf 'url = "https://www.duckdns.org/update?domains=%s&token=%s&ip="\n' \
  "$(urlencode "$duckdns_subdomain")" "$(urlencode "$duckdns_token")" > "$request_config"
chmod 0600 "$request_config"
if ! result="$(curl --disable --config "$request_config" --fail --silent --connect-timeout 10 --max-time 30 2>/dev/null)"; then
  echo "DuckDNS update request failed." >&2
  exit 1
fi
if [[ "$result" != "OK" ]]; then
  echo "DuckDNS update failed." >&2
  exit 1
fi
echo "DuckDNS updated: ${duckdns_subdomain}.duckdns.org"
