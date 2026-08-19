#!/bin/bash

#= Generic portable builder (portable, linux, macos, windows)

set -o errexit
set -o xtrace

# Set global variables
REPOSITORY_URI="https://repo.jellyfin.org"
FFMPEG_VERSION="7.x"

# Create the intermediate build dir
BUILD_DIR="/build"
mkdir -p ${BUILD_DIR}/MulletaFlix

# Move to source directory
pushd "${SOURCE_DIR}"

# Build server
pushd MulletaFlix-server
case ${BUILD_TYPE} in
    portable)
        RUNTIME=""
        APPHOST="-p:UseAppHost=false"
    ;;
    *)
        RUNTIME="--self-contained --runtime ${DOTNET_TYPE}-${DOTNET_ARCH}"
        APPHOST="-p:UseAppHost=true"
    ;;
esac
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [[ -z ${CONFIG} ]]; then
    CONFIG="Release"
fi
dotnet publish MulletaFlix.Server --configuration ${CONFIG} ${RUNTIME} --output ${BUILD_DIR}/MulletaFlix/ -p:DebugSymbols=false -p:DebugType=none ${APPHOST}
popd

# Build web
pushd MulletaFlix-web
npm ci --no-audit --unsafe-perm
npm run build:production
mv dist ${BUILD_DIR}/MulletaFlix/MulletaFlix-web
popd

mkdir -p "${ARTIFACT_DIR}/"

if [[ -n ${PACKAGE_ARCH} ]]; then
    VERSION_SUFFIX="${MulletaFlix_VERSION}-${PACKAGE_ARCH}"
else
    VERSION_SUFFIX="${MulletaFlix_VERSION}"
fi

pushd ${BUILD_DIR}

pushd MulletaFlix
# Fetch any additional package(s) needed here (i.e. FFmpeg)
# This is a hack because the ffmpeg naming is very inconsistent and we need to find the right URL(s) from the repo browser
case ${BUILD_TYPE}-${PACKAGE_ARCH} in
#    linux-amd64*)
#        FFMPEG_PATH=$( curl ${REPOSITORY_URI}/?path=/ffmpeg/linux/latest-${FFMPEG_VERSION}/amd64 | grep -o "/files/.*_portable_linux64-gpl.tar.xz'" | sed "s/'$//" )
#        curl --location --output ffmpeg.tar.xz ${REPOSITORY_URI}${FFMPEG_PATH}
#        tar -xvJf ffmpeg.tar.xz
#        rm ffmpeg.tar.xz
#    ;;
#    linux-arm64*)
#        FFMPEG_PATH=$( curl ${REPOSITORY_URI}/?path=/ffmpeg/linux/latest-${FFMPEG_VERSION}/arm64 | grep -o "/files/.*_portable_linuxarm64-gpl.tar.xz'" | sed "s/'$//" )
#        curl --location --output ffmpeg.tar.xz ${REPOSITORY_URI}${FFMPEG_PATH}
#        tar -xvJf ffmpeg.tar.xz
#        rm ffmpeg.tar.xz
#    ;;
#    macos-amd64)
#        FFMPEG_PATH=$( curl ${REPOSITORY_URI}/?path=/ffmpeg/macos/latest-${FFMPEG_VERSION}/x86_64 | grep -o "/files/.*_portable_mac64-gpl.tar.xz'" | sed "s/'$//" )
#        curl --location --output ffmpeg.tar.xz ${REPOSITORY_URI}${FFMPEG_PATH}
#        tar -xvJf ffmpeg.tar.xz
#        rm ffmpeg.tar.xz
#    ;;
#    macos-arm64)
#        FFMPEG_PATH=$( curl ${REPOSITORY_URI}/?path=/ffmpeg/macos/latest-${FFMPEG_VERSION}/arm64 | grep -o "/files/.*_portable_macarm64-gpl.tar.xz'" | sed "s/'$//" )
#        curl --location --output ffmpeg.tar.xz ${REPOSITORY_URI}${FFMPEG_PATH}
#        tar -xvJf ffmpeg.tar.xz
#        rm ffmpeg.tar.xz
#    ;;
    windows-amd64)
        FFMPEG_PATH=$( curl ${REPOSITORY_URI}/?path=/ffmpeg/windows/latest-${FFMPEG_VERSION}/win64 | grep -o "/files/.*_portable_win64-clang-gpl.zip'" | sed "s/'$//" )
        curl --location --output ffmpeg.zip ${REPOSITORY_URI}${FFMPEG_PATH}
        unzip ffmpeg.zip
        rm ffmpeg.zip

        # Download MariaDB Portable
        echo "Downloading MariaDB Portable..."
        curl --location --output mariadb.zip https://archive.mariadb.org/mariadb-11.4.2/winx64-packages/mariadb-11.4.2-winx64.zip
        unzip -q mariadb.zip
        mv mariadb-11.4.2-winx64 mariadb
        rm mariadb.zip
    ;;
    *)
        true
    ;;
esac
popd

for ARCHIVE_TYPE in $( tr ',' '\n' <<<"${ARCHIVE_TYPES}" ); do
    case ${ARCHIVE_TYPE} in
        targz)
            tar -czf "${ARTIFACT_DIR}"/MulletaFlix_${VERSION_SUFFIX}.tar.gz MulletaFlix/
        ;;
        tarxz)
            tar -cJf "${ARTIFACT_DIR}"/MulletaFlix_${VERSION_SUFFIX}.tar.xz MulletaFlix/
        ;;
        zip)
            zip -qr "${ARTIFACT_DIR}"/MulletaFlix_${VERSION_SUFFIX}.zip MulletaFlix/
        ;;
    esac
done
popd

# Clean up any lingering artifacts
make -f debian/rules clean
rm -rf ${BUILD_DIR}

popd

