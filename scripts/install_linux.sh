#!/usr/bin/env bash
# LocalLLMServerManager Linux Installer & Systemd Setup Script
set -e

INSTALL_DIR="/usr/local/share/LocalLLMServerManager"
BIN_LINK="/usr/local/bin/localllmmanager"
SERVICE_FILE="/etc/systemd/system/localllmmanager.service"
DESKTOP_FILE="/usr/share/applications/localllmmanager.desktop"
SERVICE_NAME="localllmmanager.service"
WITH_VIDEO=0
WITH_AUDIO=0

for arg in "$@"; do
  case $arg in
    --with-video)
      WITH_VIDEO=1
      shift
      ;;
    --with-audio)
      WITH_AUDIO=1
      shift
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "=========================================="
echo "  Local LLM Server Manager — Linux Setup"
echo "=========================================="
echo ""

if [ "$EUID" -ne 0 ]; then
  echo "Please run as root or with sudo: sudo ./install_linux.sh"
  exit 1
fi

# 1. Detect and stop running systemd service if active
SERVICE_WAS_ACTIVE=0
if systemctl is-active --quiet "${SERVICE_NAME}" 2>/dev/null; then
  echo "--> Detected active ${SERVICE_NAME}. Stopping service before update..."
  SERVICE_WAS_ACTIVE=1
  systemctl stop "${SERVICE_NAME}" || true
fi

# 2. Terminate any active LocalLLMServerManager processes to release file locks
if pgrep -f "LocalLLMServerManager" >/dev/null 2>&1; then
  echo "--> Stopping running LocalLLMServerManager processes..."
  pkill -f "LocalLLMServerManager" || true
  sleep 1
fi

# 3. Preserve existing settings.json so user configuration is never overwritten
SETTINGS_FILE="${INSTALL_DIR}/settings.json"
SETTINGS_BACKUP=""
if [ -f "${SETTINGS_FILE}" ]; then
  echo "--> Backing up existing settings.json..."
  SETTINGS_BACKUP="$(mktemp)"
  cp "${SETTINGS_FILE}" "${SETTINGS_BACKUP}"
fi

# 4. Build and publish self-contained release
echo "--> Building self-contained release..."
mkdir -p "${INSTALL_DIR}"

if [ -f "${ROOT_DIR}/LocalLLMServerManager.csproj" ]; then
  dotnet publish "${ROOT_DIR}/LocalLLMServerManager.csproj" -c Release -r linux-x64 --self-contained -o "${INSTALL_DIR}" --nologo /p:PublishSingleFile=false
elif [ -f "${SCRIPT_DIR}/LocalLLMServerManager.csproj" ]; then
  dotnet publish "${SCRIPT_DIR}/LocalLLMServerManager.csproj" -c Release -r linux-x64 --self-contained -o "${INSTALL_DIR}" --nologo /p:PublishSingleFile=false
else
  dotnet publish -c Release -r linux-x64 --self-contained -o "${INSTALL_DIR}" --nologo /p:PublishSingleFile=false
fi

chmod +x "${INSTALL_DIR}/LocalLLMServerManager"

# 5. Restore preserved settings.json
if [ -n "${SETTINGS_BACKUP}" ] && [ -f "${SETTINGS_BACKUP}" ]; then
  echo "--> Restoring preserved settings.json..."
  cp "${SETTINGS_BACKUP}" "${SETTINGS_FILE}"
  rm -f "${SETTINGS_BACKUP}"
  chmod 666 "${SETTINGS_FILE}" 2>/dev/null || true
fi

# 6. Create symlink in /usr/local/bin
echo "--> Creating symlink in /usr/local/bin..."
ln -sf "${INSTALL_DIR}/LocalLLMServerManager" "${BIN_LINK}"

# 7. Install Desktop launcher if file exists
if [ -f "${SCRIPT_DIR}/localllmmanager.desktop" ]; then
  echo "--> Installing Desktop launcher..."
  cp "${SCRIPT_DIR}/localllmmanager.desktop" "${DESKTOP_FILE}"
  chmod 644 "${DESKTOP_FILE}"
fi

# 8. Install systemd service unit if file exists
if [ -f "${SCRIPT_DIR}/localllmmanager.service" ]; then
  echo "--> Installing systemd service..."
  cp "${SCRIPT_DIR}/localllmmanager.service" "${SERVICE_FILE}"
  chmod 644 "${SERVICE_FILE}"
fi

# 9. Optional Feature Pack Installation
if [ "${WITH_VIDEO}" -eq 1 ]; then
  echo "--> Installing Video Generation Feature Pack..."
  mkdir -p "${INSTALL_DIR}/Workflows/Video"
fi

if [ "${WITH_AUDIO}" -eq 1 ]; then
  echo "--> Installing Audio & Kokoro TTS Feature Pack..."
  mkdir -p "${INSTALL_DIR}/kokoro-fastapi"
  mkdir -p "${INSTALL_DIR}/models/audio"
fi

# 10. Reload systemd daemon and restart service if it was previously running
echo "--> Reloading systemd daemon..."
systemctl daemon-reload

if [ "${SERVICE_WAS_ACTIVE}" -eq 1 ] || systemctl is-enabled --quiet "${SERVICE_NAME}" 2>/dev/null; then
  echo "--> Starting/Restarting ${SERVICE_NAME}..."
  systemctl restart "${SERVICE_NAME}" || true
fi

echo ""
echo "=========================================="
echo "  Installation Complete!"
echo "=========================================="
echo "To start native desktop app: localllmmanager"
echo "To enable background systemd service: sudo systemctl enable --now localllmmanager"
echo "Web Dashboard will run at: http://localhost:5246"
echo "=========================================="
