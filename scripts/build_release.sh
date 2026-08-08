#!/usr/bin/env bash
# LocalLLMServerManager v3.1.0 — Linux Release Build & Package Script
set -e

VERSION="3.3.0"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
PUBLISH_DIR="${ROOT_DIR}/publish"
DIST_DIR="${ROOT_DIR}/dist"
TAR_FILE="${DIST_DIR}/LocalLLMServerManager-v${VERSION}-linux-x64.tar.gz"

echo "=========================================="
echo "  Building Linux Release Package v${VERSION}"
echo "=========================================="
echo ""

# 1. Run Verification & Test Suite
echo "--> 1. Running Test Suite & Verification..."
dotnet test LocalLLMServerManager.sln --nologo -c Release

# 2. Publish Avalonia WebAssembly to wwwroot & Build Self-Contained Release
echo "--> 2. Building & Publishing Avalonia WebAssembly (Wasm) UI..."
rm -rf "${ROOT_DIR}/wwwroot_wasm"
dotnet publish LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj -c Release -o "${ROOT_DIR}/wwwroot_wasm" --nologo

rm -f "${ROOT_DIR}/wwwroot/app.js" "${ROOT_DIR}/wwwroot/index.css" "${ROOT_DIR}/wwwroot/index.html"
rm -rf "${ROOT_DIR}/wwwroot/wwwroot"
cp -r "${ROOT_DIR}/wwwroot_wasm/wwwroot/"* "${ROOT_DIR}/wwwroot/"
rm -rf "${ROOT_DIR}/wwwroot_wasm"

echo "--> 3. Publishing Self-Contained linux-x64 Executable..."
rm -rf "${PUBLISH_DIR}" "${DIST_DIR}"
mkdir -p "${DIST_DIR}"

dotnet publish LocalLLMServerManager.csproj -c Release -r linux-x64 --self-contained -o "${PUBLISH_DIR}" --nologo /p:PublishSingleFile=false
chmod +x "${PUBLISH_DIR}/LocalLLMServerManager"

# 3. Create Release Archive
echo "--> 4. Creating Release Tarball Archive..."
tar -czf "${TAR_FILE}" -C "${PUBLISH_DIR}" .

echo "Tarball created: ${TAR_FILE}"
echo ""
echo "=========================================="
echo "  Linux Release Build Complete!"
echo "  Outputs located in: ${DIST_DIR}"
echo "=========================================="
