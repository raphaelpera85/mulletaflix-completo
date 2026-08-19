#!/bin/bash
# build-installer.sh - Build MulletaFlix Windows NSIS Installer
# Run from project root: ./build-installer.sh

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UX_CUSTOM="$PROJECT_ROOT/MulletaFlix-packaging-master/MulletaFlix-ux-custom"
NSIS_SCRIPT="$UX_CUSTOM/nsis/mulletaflix.nsi"
STAGE_DIR="$PROJECT_ROOT/stage"

echo "=================================================="
echo "  MulletaFlix NSIS Installer Builder"
echo "=================================================="
echo ""

# Check prerequisites
if ! command -v makensis &> /dev/null; then
    echo "ERROR: makensis not found in PATH."
    echo "Install NSIS 3.x (winget install NSIS or download from nsis.sourceforge.io)"
    exit 1
fi

if [[ ! -d "$STAGE_DIR" ]]; then
    echo "ERROR: Stage directory not found: $STAGE_DIR"
    echo "Run build-stage-and-installer.ps1 first to prepare the stage folder."
    exit 1
fi

if [[ ! -f "$NSIS_SCRIPT" ]]; then
    echo "ERROR: NSIS script not found: $NSIS_SCRIPT"
    exit 1
fi

# Verify required files in stage
REQUIRED=("MulletaFlix.exe" "MulletaFlix.dll" "icon.ico")
for file in "${REQUIRED[@]}"; do
    if [[ ! -f "$STAGE_DIR/$file" ]]; then
        echo "WARNING: Required file missing from stage: $STAGE_DIR/$file"
    fi
done

if [[ ! -d "$STAGE_DIR/mulletaflix-windows-tray" ]]; then
    echo "WARNING: Tray app directory missing from stage"
fi

# Build
echo "Compiling NSIS installer..."
makensis \
    /Dx64 \
    /DUXPATH="$UX_CUSTOM" \
    /DInstallLocation="$STAGE_DIR" \
    "$NSIS_SCRIPT"

# Find output
OUTPUT_DIR="$PROJECT_ROOT/MulletaFlix-packaging-master/jellyfin-server-windows/nsis"
INSTALLER=$(ls -t "$OUTPUT_DIR"/mulletaflix_*_windows-x64.exe 2>/dev/null | head -1)

if [[ -z "$INSTALLER" ]]; then
    echo "ERROR: Installer not found after compilation"
    exit 1
fi

SIZE_MB=$(du -m "$INSTALLER" | cut -f1)

echo ""
echo "=================================================="
echo "Installer build completed successfully!"
echo "Output: $INSTALLER"
echo "Size: ${SIZE_MB} MB"
echo "=================================================="