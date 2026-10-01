#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
version="$(sed -n 's/.*AssemblyFileVersion("\([^"]*\)").*/\1/p' "$repo_root/MulletaFlix-master/SharedVersion.cs" | head -n 1)"
if [[ -z "$version" ]]; then
  echo "Could not read server version from SharedVersion.cs" >&2
  exit 1
fi

out_dir="${1:-$repo_root/dist}"
mkdir -p "$out_dir"
tmp_root="${TMPDIR:-/tmp}"
work_dir="$(mktemp -d "$tmp_root/mulletaflix-linux-build.XXXXXXXX")"
cleanup() {
  case "$work_dir" in
    "$tmp_root"/mulletaflix-linux-build.*) rm -rf -- "$work_dir" ;;
    *) echo "Refusing to remove unexpected build temp path: $work_dir" >&2; return 1 ;;
  esac
}
trap cleanup EXIT
app_dir="$work_dir/mulletaflix-linux-x64"
mkdir -p "$app_dir"

dotnet publish "$repo_root/MulletaFlix-master/Jellyfin.Server/Jellyfin.Server.csproj" \
  --configuration Release --runtime linux-x64 --self-contained true \
  --output "$app_dir/server" -p:DebugSymbols=false -p:DebugType=none \
  -p:GenerateDocumentationFile=false -p:RunAnalyzersDuringBuild=false -p:RunAnalyzers=false

plugin_version='3.0.1.0'
plugin_archive="$work_dir/file-transformation.zip"
plugin_source='https://github.com/IAmParadox27/jellyfin-plugin-file-transformation/releases/download/3.0.1.0/Release-12.1.0.zip'
plugin_sha256='C1318B2438F4C0DBFD46850BCBD3A5C18ECAF6F873A4790318693E0D3DBAA7B3'
plugin_dll_sha256='BB941BFD775369F6AC1659CB64AF12DD62AD8F6D74981EB67179D44F26F79590'
license_url='https://raw.githubusercontent.com/IAmParadox27/jellyfin-plugin-file-transformation/3.0.1.0/LICENSE'
license_sha256='3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986'
plugin_extract="$work_dir/file-transformation"
plugin_bundle="$app_dir/server/bundled-plugins/FileTransformation_$plugin_version"
curl --fail --location --silent --show-error "$plugin_source" --output "$plugin_archive"
printf '%s  %s\n' "$plugin_sha256" "$plugin_archive" | sha256sum --check --status
curl --fail --location --silent --show-error "$license_url" --output "$work_dir/file-transformation.LICENSE"
printf '%s  %s\n' "$license_sha256" "$work_dir/file-transformation.LICENSE" | sha256sum --check --status
mkdir -p "$plugin_extract" "$plugin_bundle"
unzip -q "$plugin_archive" -d "$plugin_extract"
for plugin_file in Jellyfin.Plugin.FileTransformation.dll Jellyfin.Plugin.FileTransformation.deps.json logo.png; do
  test -f "$plugin_extract/$plugin_file"
  cp "$plugin_extract/$plugin_file" "$plugin_bundle/$plugin_file"
done
printf '%s  %s\n' "$plugin_dll_sha256" "$plugin_bundle/Jellyfin.Plugin.FileTransformation.dll" | sha256sum --check --status
cp "$work_dir/file-transformation.LICENSE" "$plugin_bundle/LICENSE-GPL-3.0.txt"
cat > "$plugin_bundle/meta.json" <<'EOF'
{
  "category": "General",
  "changelog": "Bundled with MulletaFlix; package source: jellyfin-plugin-file-transformation 3.0.1.0 (Release-12.1.0).",
  "description": "Applies supported transformations to the Jellyfin web interface. Required by Intro Skipper web controls.",
  "guid": "5e87cc92-571a-4d8d-8d98-d2d4147f9f90",
  "name": "File Transformation",
  "overview": "Web interface transformation middleware used by Intro Skipper.",
  "owner": "IAmParadox27",
  "targetAbi": "12.1.0.0",
  "version": "3.0.1.0",
  "status": "Active",
  "autoUpdate": false,
  "imagePath": "logo.png",
  "assemblies": ["Jellyfin.Plugin.FileTransformation.dll"]
}
EOF

pushd "$repo_root/MulletaFlix-web-master" >/dev/null
web_build_dir="$work_dir/web-source"
mkdir -p "$web_build_dir"
tar -cf - --exclude='*node_modules*' --exclude='./dist' --exclude='./dist/**' --exclude='*/dist' --exclude='*/dist/**' \
  --exclude='*.git*' --exclude='*.vite*' -C "$repo_root/MulletaFlix-web-master" . \
  | tar -xf - -C "$web_build_dir"
popd >/dev/null
pushd "$web_build_dir" >/dev/null
npm ci
npm run build:check
npm run build:production
popd >/dev/null
cp -a "$web_build_dir/dist" "$app_dir/server/MulletaFlix-web"

cp "$repo_root/tools/release/linux-install.sh" "$app_dir/install.sh"
cp "$repo_root/tools/release/mulletaflix.service" "$app_dir/mulletaflix.service"
cp "$repo_root/tools/release/LINUX-README.md" "$app_dir/README.md"

archive="$out_dir/mulletaflix_${version}_linux-x64.tar.gz"
tar -czf "$archive" -C "$work_dir" "$(basename "$app_dir")"
sha256sum "$archive"
echo "Linux package created: $archive"
