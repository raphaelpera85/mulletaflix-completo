#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run as root: sudo ./install.sh" >&2
  exit 1
fi
if ! command -v apt-get >/dev/null 2>&1 || ! command -v systemctl >/dev/null 2>&1; then
  echo "Supported installer target: Debian/Ubuntu with systemd." >&2
  exit 1
fi

package_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ ! -x "$package_root/server/MulletaFlix" && ! -f "$package_root/server/MulletaFlix" ]]; then
  echo "Server payload missing: $package_root/server/MulletaFlix" >&2
  exit 1
fi

public_host="${MULLETAFLIX_PUBLIC_HOST:-}"
acme_email="${MULLETAFLIX_ACME_EMAIL:-}"
duckdns_subdomain="${MULLETAFLIX_DUCKDNS_SUBDOMAIN:-}"
duckdns_token="${MULLETAFLIX_DUCKDNS_TOKEN:-}"
skip_https="${MULLETAFLIX_SKIP_HTTPS:-0}"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --https-host) public_host="${2:?missing hostname}"; shift 2 ;;
    --https-email) acme_email="${2:?missing ACME email}"; shift 2 ;;
    --duckdns-subdomain) duckdns_subdomain="${2:?missing DuckDNS subdomain}"; shift 2 ;;
    --duckdns-token) duckdns_token="${2:?missing DuckDNS token}"; shift 2 ;;
    --skip-https) skip_https=1; shift ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ -n "$duckdns_subdomain" ]]; then
  if [[ ! "$duckdns_subdomain" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$ ]]; then
    echo "Invalid DuckDNS subdomain." >&2
    exit 1
  fi
  if [[ -z "$duckdns_token" ]]; then
    echo "DuckDNS token is required when --duckdns-subdomain is used." >&2
    exit 1
  fi
  public_host="${duckdns_subdomain}.duckdns.org"
fi
if [[ -n "$public_host" && ! "$public_host" =~ ^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$ ]]; then
  echo "Invalid public hostname." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
packages=(mariadb-server mariadb-client ffmpeg ca-certificates openssl curl)
if [[ "$skip_https" != "1" && -n "$public_host" ]]; then
  packages+=(nginx certbot)
fi
apt-get install -y "${packages[@]}"
systemctl enable --now mariadb

if ! getent group mulletaflix >/dev/null; then
  groupadd --system mulletaflix
fi
if ! id mulletaflix >/dev/null 2>&1; then
  useradd --system --gid mulletaflix --home-dir /var/lib/mulletaflix --create-home --shell /usr/sbin/nologin mulletaflix
fi

install -d -o root -g mulletaflix -m 0750 /etc/mulletaflix
install -d -o mulletaflix -g mulletaflix -m 0750 /etc/mulletaflix/config
install -d -o mulletaflix -g mulletaflix -m 0750 /var/lib/mulletaflix /var/cache/mulletaflix /var/log/mulletaflix

db_password=""
if [[ -f /etc/mulletaflix/server.env ]]; then
  db_password="$(sed -n 's/^MULLETAFLIX_DB_PASSWORD=\([0-9a-f]*\)$/\1/p' /etc/mulletaflix/server.env | head -n 1)"
fi
if [[ ! "$db_password" =~ ^[0-9a-f]{64}$ ]]; then
  db_password="$(openssl rand -hex 32)"
fi

mariadb --protocol=socket -uroot <<SQL
CREATE DATABASE IF NOT EXISTS mulletaflix CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_users CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_movies CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_series CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_channels CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_books CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS mulletaflix_introskipper CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'mulletaflix'@'127.0.0.1' IDENTIFIED BY '${db_password}';
ALTER USER 'mulletaflix'@'127.0.0.1' IDENTIFIED BY '${db_password}';
GRANT ALL PRIVILEGES ON mulletaflix.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_users.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_movies.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_series.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_channels.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_books.* TO 'mulletaflix'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mulletaflix_introskipper.* TO 'mulletaflix'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL

printf 'MULLETAFLIX_DB_SERVER=127.0.0.1\nMULLETAFLIX_DB_PORT=3306\nMULLETAFLIX_DB_USER=mulletaflix\nMULLETAFLIX_DB_PASSWORD=%s\n' "$db_password" > /etc/mulletaflix/server.env
chmod 0640 /etc/mulletaflix/server.env
chown root:mulletaflix /etc/mulletaflix/server.env

install -d -m 0755 /opt/mulletaflix/server
cp -a "$package_root/server/." /opt/mulletaflix/server/
chmod 0755 /opt/mulletaflix/server/MulletaFlix
chown -R root:root /opt/mulletaflix
install -m 0644 "$package_root/mulletaflix.service" /etc/systemd/system/mulletaflix.service
systemctl daemon-reload
systemctl enable --now mulletaflix.service

if [[ -n "$duckdns_subdomain" ]]; then
  install -m 0600 /dev/null /etc/mulletaflix/duckdns.env
  printf 'DUCKDNS_SUBDOMAIN=%q\nDUCKDNS_TOKEN=%q\n' "$duckdns_subdomain" "$duckdns_token" > /etc/mulletaflix/duckdns.env
  install -m 0750 "$package_root/duckdns-update.sh" /usr/local/sbin/mulletaflix-duckdns-update
  cat > /etc/systemd/system/mulletaflix-duckdns.service <<'EOF'
[Unit]
Description=MulletaFlix DuckDNS updater

[Service]
Type=oneshot
ExecStart=/usr/local/sbin/mulletaflix-duckdns-update /etc/mulletaflix/duckdns.env
EOF
  cat > /etc/systemd/system/mulletaflix-duckdns.timer <<'EOF'
[Unit]
Description=Update MulletaFlix DuckDNS address

[Timer]
OnBootSec=1min
OnUnitActiveSec=5min
Persistent=true

[Install]
WantedBy=timers.target
EOF
  systemctl daemon-reload
  systemctl enable --now mulletaflix-duckdns.timer
  /usr/local/sbin/mulletaflix-duckdns-update /etc/mulletaflix/duckdns.env
fi

if [[ "$skip_https" != "1" && -n "$public_host" ]]; then
  if [[ -z "$acme_email" ]]; then
    echo "HTTPS requested but --https-email or MULLETAFLIX_ACME_EMAIL was not provided." >&2
    exit 1
  fi

  nginx_root=/etc/nginx
  acme_root=/var/www/mulletaflix-acme
  site_path="$nginx_root/sites-available/mulletaflix"
  install -d -m 0755 "$acme_root/.well-known/acme-challenge"
  cat > "$site_path" <<EOF
map \$http_upgrade \$connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 80;
    server_name $public_host;

    location /.well-known/acme-challenge/ {
        root $acme_root;
        try_files \$uri =404;
    }

    location / {
        proxy_pass http://127.0.0.1:8096;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto http;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_read_timeout 3600;
        proxy_buffering off;
    }
}
EOF
  ln -sfn "$site_path" "$nginx_root/sites-enabled/mulletaflix"
  nginx -t
  systemctl enable --now nginx
  certbot certonly --webroot -w "$acme_root" -d "$public_host" --email "$acme_email" --agree-tos --non-interactive --no-eff-email

  cat > "$site_path" <<EOF
map \$http_upgrade \$connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 80;
    server_name $public_host;
    location /.well-known/acme-challenge/ {
        root $acme_root;
        try_files \$uri =404;
    }
    location / { return 301 https://\$host\$request_uri; }
}

server {
    listen 443 ssl;
    server_name $public_host;
    ssl_certificate /etc/letsencrypt/live/$public_host/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$public_host/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    location / {
        proxy_pass http://127.0.0.1:8096;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_read_timeout 3600;
        proxy_buffering off;
    }
}
EOF
  install -d -m 0755 /etc/letsencrypt/renewal-hooks/deploy
  cat > /etc/letsencrypt/renewal-hooks/deploy/mulletaflix-nginx-reload <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
systemctl reload nginx
EOF
  chmod 0755 /etc/letsencrypt/renewal-hooks/deploy/mulletaflix-nginx-reload
  nginx -t
  systemctl reload nginx
  echo "MulletaFlix HTTPS configured for https://$public_host"
else
  echo "HTTPS not configured. Re-run with --https-host <hostname> --https-email <email>."
fi

echo "MulletaFlix installed. Open http://<server-ip>:8096/"
