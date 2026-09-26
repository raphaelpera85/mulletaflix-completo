#!/usr/bin/env bash
set -euo pipefail

IOS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEVICE_NAME="${1:-iPhone 16}"

if ! command -v xcrun >/dev/null 2>&1 || ! command -v xcodebuild >/dev/null 2>&1 || ! command -v swift >/dev/null 2>&1; then
  echo "Xcode, Swift e o iOS Simulator precisam estar instalados em um Mac." >&2
  exit 1
fi

DEVICE_ID="$(xcrun simctl list devices available | awk -F '[()]' -v name="$DEVICE_NAME" '$0 ~ name && $0 ~ /(Shutdown|Booted)/ { print $2; exit }')"
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(xcrun simctl list devices available | awk -F '[()]' '/iPhone/ && /(Shutdown|Booted)/ { print $2; exit }')"
fi
if [[ -z "$DEVICE_ID" ]]; then
  echo "Nenhum iPhone Simulator disponível. Crie um dispositivo pelo Xcode > Window > Devices and Simulators." >&2
  exit 1
fi

xcrun simctl boot "$DEVICE_ID" 2>/dev/null || true
open -a Simulator

echo "Executando testes Swift do núcleo..."
(cd "$IOS_ROOT" && swift test --enable-code-coverage)

xcodebuild \
  -project "$IOS_ROOT/MulletaFlix.xcodeproj" \
  -scheme MulletaFlix \
  -sdk iphonesimulator \
  -destination "id=$DEVICE_ID" \
  -configuration Debug \
  CODE_SIGNING_ALLOWED=NO \
  -resultBundlePath "$IOS_ROOT/xcodebuild-results.xcresult" \
  build

echo "Simulator pronto: $DEVICE_NAME ($DEVICE_ID)"
