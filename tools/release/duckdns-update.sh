#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-/etc/mulletaflix/duckdns.env}"
if [[ ! -r "$env_file" ]]; then
  echo "DuckDNS configuration not found: $env_file" >&2
  exit 1
fi
# shellcheck disable=SC1090
source "$env_file"
: "${DUCKDNS_SUBDOMAIN:?DUCKDNS_SUBDOMAIN is required}"
: "${DUCKDNS_TOKEN:?DUCKDNS_TOKEN is required}"
if [[ ! "$DUCKDNS_SUBDOMAIN" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$ ]]; then
  echo "Invalid DuckDNS subdomain." >&2
  exit 1
fi
result="$(curl --fail --silent --show-error --get \
  --data-urlencode "domains=$DUCKDNS_SUBDOMAIN" \
  --data-urlencode "token=$DUCKDNS_TOKEN" \
  --data-urlencode "ip=" \
  https://www.duckdns.org/update)"
if [[ "$result" != "OK" ]]; then
  echo "DuckDNS update failed: $result" >&2
  exit 1
fi
echo "DuckDNS updated: ${DUCKDNS_SUBDOMAIN}.duckdns.org"
