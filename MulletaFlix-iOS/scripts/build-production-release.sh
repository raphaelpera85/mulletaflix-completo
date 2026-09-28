#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "Este script exige macOS/Xcode para assinar uma release iOS." >&2
  exit 2
fi

command -v xcodebuild >/dev/null || { echo "xcodebuild não encontrado." >&2; exit 2; }
command -v swift >/dev/null || { echo "swift não encontrado." >&2; exit 2; }

EXPORT_OPTIONS_PLIST="${EXPORT_OPTIONS_PLIST:-}"
if [[ -z "$EXPORT_OPTIONS_PLIST" || ! -f "$EXPORT_OPTIONS_PLIST" ]]; then
  echo "Defina EXPORT_OPTIONS_PLIST apontando para um ExportOptions.plist de distribuição." >&2
  exit 2
fi

EXPORT_METHOD="$(/usr/libexec/PlistBuddy -c 'Print :method' "$EXPORT_OPTIONS_PLIST" 2>/dev/null || true)"
case "$EXPORT_METHOD" in
  app-store|ad-hoc|enterprise) ;;
  *)
    echo "ExportOptions.plist deve usar um método de distribuição (app-store, ad-hoc ou enterprise), não '$EXPORT_METHOD'." >&2
    exit 2
    ;;
esac

SIGNING_STYLE="$(/usr/libexec/PlistBuddy -c 'Print :signingStyle' "$EXPORT_OPTIONS_PLIST" 2>/dev/null || true)"
case "$SIGNING_STYLE" in
  automatic|manual) ;;
  *)
    echo "ExportOptions.plist deve declarar signingStyle como automatic ou manual, não '$SIGNING_STYLE'." >&2
    exit 2
    ;;
esac

TEAM_ID="$(/usr/libexec/PlistBuddy -c 'Print :teamID' "$EXPORT_OPTIONS_PLIST" 2>/dev/null || true)"
if [[ -z "$TEAM_ID" || "$TEAM_ID" == REPLACE_WITH_APPLE_TEAM_ID ]]; then
  echo "ExportOptions.plist deve conter o Team ID real da Apple; o placeholder não pode ser usado em produção." >&2
  exit 2
fi

SCHEME="MulletaFlix"
ARCHIVE_PATH="${ARCHIVE_PATH:-$ROOT_DIR/build/MulletaFlix.xcarchive}"
EXPORT_DIR="${EXPORT_DIR:-$ROOT_DIR/build/export}"
DIST_DIR="${DIST_DIR:-$ROOT_DIR/../dist}"

rm -rf "$ARCHIVE_PATH" "$EXPORT_DIR"
mkdir -p "$(dirname "$ARCHIVE_PATH")" "$EXPORT_DIR" "$DIST_DIR"

echo "== Swift Core tests =="
swift test --enable-code-coverage

echo "== Release archive for physical iOS device =="
xcodebuild \
  -project MulletaFlix.xcodeproj \
  -scheme "$SCHEME" \
  -configuration Release \
  -destination 'generic/platform=iOS' \
  -archivePath "$ARCHIVE_PATH" \
  archive

if [[ ! -f "$ARCHIVE_PATH/Info.plist" ]]; then
  echo "Archive não foi criado." >&2
  exit 1
fi

ARCHIVE_INFO="$ARCHIVE_PATH/Info.plist"
PRODUCT_TYPE="$(/usr/libexec/PlistBuddy -c 'Print :ApplicationProperties:ApplicationPath' "$ARCHIVE_INFO" 2>/dev/null || true)"
if [[ -z "$PRODUCT_TYPE" || "$PRODUCT_TYPE" == *"iphonesimulator"* ]]; then
  echo "O archive não é um produto iOS de dispositivo físico." >&2
  exit 1
fi

APP_PATH="$ARCHIVE_PATH/Products/Applications/MulletaFlix.app"
if [[ ! -d "$APP_PATH" ]]; then
  echo "Aplicativo não encontrado dentro do archive." >&2
  exit 1
fi
codesign --verify --deep --strict "$APP_PATH"
CODESIGN_DETAILS="$(codesign -dv --verbose=4 "$APP_PATH" 2>&1)"
if ! grep -Eq 'Authority=(Apple Distribution|iPhone Distribution):' <<<"$CODESIGN_DETAILS"; then
  echo "O archive não possui uma autoridade de assinatura de distribuição Apple." >&2
  exit 1
fi
if ! grep -Eq '^TeamIdentifier=' <<<"$CODESIGN_DETAILS"; then
  echo "O archive não expõe um TeamIdentifier de assinatura." >&2
  exit 1
fi
ENTITLEMENTS="$(codesign -d --entitlements :- "$APP_PATH" 2>/dev/null || true)"
if grep -Eq '<key>get-task-allow</key>[[:space:]]*<true/>' <<<"$ENTITLEMENTS"; then
  echo "O archive contém get-task-allow=true e não pode ser publicado como produção." >&2
  exit 1
fi

echo "== Export signed distribution IPA =="
xcodebuild \
  -exportArchive \
  -archivePath "$ARCHIVE_PATH" \
  -exportPath "$EXPORT_DIR" \
  -exportOptionsPlist "$EXPORT_OPTIONS_PLIST"

IPA_PATH="$(find "$EXPORT_DIR" -maxdepth 1 -type f -name '*.ipa' -print -quit)"
if [[ -z "$IPA_PATH" || ! -f "$IPA_PATH" ]]; then
  echo "Nenhum IPA foi exportado." >&2
  exit 1
fi

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :ApplicationProperties:CFBundleShortVersionString' "$ARCHIVE_INFO")"
BUILD="$(/usr/libexec/PlistBuddy -c 'Print :ApplicationProperties:CFBundleVersion' "$ARCHIVE_INFO")"
EXPECTED_IPA="$DIST_DIR/mulletaflix-ios-v${VERSION}.ipa"
cp "$IPA_PATH" "$EXPECTED_IPA"

RUNTIME_VERSION="$(sed -n 's/.*public static let version = "\([^"]*\)".*/\1/p' Sources/MulletaFlixCore/AppIdentity.swift | head -n 1)"
if [[ -z "$RUNTIME_VERSION" || "$RUNTIME_VERSION" != "$VERSION" ]]; then
  echo "A versão do runtime (${RUNTIME_VERSION:-ausente}) não coincide com a versão do bundle (${VERSION})." >&2
  rm -f "$EXPECTED_IPA"
  exit 1
fi

SHA256="$(shasum -a 256 "$EXPECTED_IPA" | awk '{print toupper($1)}')"
BYTES="$(stat -f '%z' "$EXPECTED_IPA")"
printf 'IPA=%s\nVERSION=%s\nBUILD=%s\nBYTES=%s\nSHA256=%s\n' \
  "$EXPECTED_IPA" "$VERSION" "$BUILD" "$BYTES" "$SHA256"
