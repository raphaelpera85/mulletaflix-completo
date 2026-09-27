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

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y mariadb-server mariadb-client ffmpeg ca-certificates openssl
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

echo "MulletaFlix installed. Open http://<server-ip>:8096/"
