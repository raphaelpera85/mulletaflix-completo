# MulletaFlix Server for Linux (x86_64)

Supported installation target: Debian/Ubuntu x86_64 using systemd. This package is self-contained for .NET; MariaDB and FFmpeg are installed from the distribution repositories by the installer.

## Install

```bash
tar -xzf mulletaflix_*_linux-x64.tar.gz
cd mulletaflix-linux-x64
sudo ./install.sh
```

The installer creates a dedicated `mulletaflix` system account, configures a local MariaDB account with a generated password, installs the systemd service, and starts the server. Application data is stored in `/var/lib/mulletaflix`; configuration is stored in `/etc/mulletaflix`; logs are in `/var/log/mulletaflix`.

Open `http://<server-ip>:8096/` and complete the first-run wizard. Do not expose plain HTTP directly to the public internet; use a VPN or HTTPS reverse proxy and a restrictive firewall.

## Service operations

```bash
sudo systemctl status mulletaflix
sudo systemctl restart mulletaflix
sudo journalctl -u mulletaflix -f
```

The package does not migrate an existing Windows installation or its database automatically. Back up and migrate data separately before switching platforms.
