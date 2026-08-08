#!/usr/bin/env bash
# LocalLLMServerManager Linux Installer & Systemd Setup Script
set -e

INSTALL_DIR="/usr/local/share/LocalLLMServerManager"
BIN_LINK="/usr/local/bin/localllmmanager"
SERVICE_FILE="/etc/systemd/system/localllmmanager.service"
DESKTOP_FILE="/usr/share/applications/localllmmanager.desktop"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================="
echo "  Local LLM Server Manager — Linux Setup"
echo "=========================================="
echo ""

if [ "$EUID" -ne 0 ]; then
  echo "Please run as root or with sudo: sudo ./install_linux.sh"
  exit 1
fi

echo "--> 1. Building self-contained release..."
dotnet publish "${SCRIPT_DIR}/LocalLLMServerManager.csproj" -c Release -r linux-x64 --self-contained -o "${INSTALL_DIR}" --nologo /p:PublishSingleFile=false
chmod +x "${INSTALL_DIR}/LocalLLMServerManager"

echo "--> 2. Creating symlink in /usr/local/bin..."
ln -sf "${INSTALL_DIR}/LocalLLMServerManager" "${BIN_LINK}"

echo "--> 3. Installing Desktop launcher..."
cp "${SCRIPT_DIR}/localllmmanager.desktop" "${DESKTOP_FILE}"
chmod 644 "${DESKTOP_FILE}"

echo "--> 4. Installing systemd service..."
cp "${SCRIPT_DIR}/localllmmanager.service" "${SERVICE_FILE}"
chmod 644 "${SERVICE_FILE}"

systemctl daemon-reload

echo ""
echo "=========================================="
echo "  Installation Complete!"
echo "=========================================="
echo "To start native desktop app: localllmmanager"
echo "To enable background systemd service: sudo systemctl enable --now localllmmanager"
echo "Web Dashboard will run at: http://localhost:5246"
echo "=========================================="
