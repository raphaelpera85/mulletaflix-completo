# MulletaFlix Server for Linux (x86_64)

Supported installation target: Debian/Ubuntu x86_64 using systemd. This package is self-contained for .NET; MariaDB and FFmpeg are installed from the distribution repositories by the installer.

## Install

```bash
tar -xzf mulletaflix_*_linux-x64.tar.gz
cd mulletaflix-linux-x64
sudo ./install.sh
```

To configure a DuckDNS hostname, pass the subdomain and the token generated in your DuckDNS account. The installer updates the public IP automatically every five minutes and can issue the Let's Encrypt certificate in the same run:

```bash
sudo ./install.sh --duckdns-subdomain mulletaflix --duckdns-token '<DUCKDNS_TOKEN>' \
  --https-email raphaelpera85@gmail.com
```

This produces `mulletaflix.duckdns.org`. The token is stored in `/etc/mulletaflix/duckdns.env` with mode `0600`; the updater is managed by `mulletaflix-duckdns.timer`.

The installer creates a dedicated `mulletaflix` system account, configures a local MariaDB account with a generated password, installs the systemd service, and starts the server. Application data is stored in `/var/lib/mulletaflix`; configuration is stored in `/etc/mulletaflix`; logs are in `/var/log/mulletaflix`.

Open `http://<server-ip>:8096/` and complete the first-run wizard. Do not expose plain HTTP directly to the public internet; use a VPN or HTTPS reverse proxy and a restrictive firewall.

## Service operations

```bash
sudo systemctl status mulletaflix
sudo systemctl restart mulletaflix
sudo journalctl -u mulletaflix -f
```

The package does not migrate an existing Windows installation or its database automatically. Back up and migrate data separately before switching platforms. DuckDNS and Let's Encrypt require a public DNS name, a DuckDNS token, and router/firewall forwarding of TCP ports 80 and 443 to this server; the installer cannot change the router automatically.
