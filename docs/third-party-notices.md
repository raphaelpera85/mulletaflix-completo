# Third-party notices

## File Transformation plugin

MulletaFlix server packages include the official `File Transformation` plugin by IAmParadox27, version `3.0.1.0`, built for the Jellyfin `12.1.0` release line. Intro Skipper uses this plugin's web-response transformation API to expose its configuration and playback controls in the web client.

- Source: [jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)
- License: GNU General Public License v3.0 (GPL-3.0)
- Upstream asset: `Release-12.1.0.zip`
- Pinned SHA-256: `C1318B2438F4C0DBFD46850BCBD3A5C18ECAF6F873A4790318693E0D3DBAA7B3`

The build scripts download the upstream release asset and license, verify both pinned checksums, and package its plugin assembly, dependency manifest, logo, and `LICENSE-GPL-3.0.txt`. They omit the upstream PDB symbols. On first server startup, the bundled plugin is copied to the server data plugin directory before plugin discovery. An already installed equal or newer version is left untouched.
