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

pushd "$repo_root/MulletaFlix-web-master" >/dev/null
npm ci
npm run build:production
popd >/dev/null
cp -a "$repo_root/MulletaFlix-web-master/dist" "$app_dir/server/MulletaFlix-web"

cp "$repo_root/tools/release/linux-install.sh" "$app_dir/install.sh"
cp "$repo_root/tools/release/mulletaflix.service" "$app_dir/mulletaflix.service"
cp "$repo_root/tools/release/LINUX-README.md" "$app_dir/README.md"

archive="$out_dir/mulletaflix_${version}_linux-x64.tar.gz"
tar -czf "$archive" -C "$work_dir" "$(basename "$app_dir")"
sha256sum "$archive"
echo "Linux package created: $archive"
