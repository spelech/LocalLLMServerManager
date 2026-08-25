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

while [ $# -gt 0 ]; do
  case "$1" in
    --with-video)
      WITH_VIDEO=1
      shift
      ;;
    --with-audio)
      WITH_AUDIO=1
      shift
      ;;
    *)
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

# 5. Check & Install Prerequisites (FFmpeg)
echo "--> Checking FFmpeg prerequisite..."
if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "--> FFmpeg not found. Attempting package installation..."
  if command -v apt-get >/dev/null 2>&1; then
    apt-get update && apt-get install -y ffmpeg || true
  elif command -v dnf >/dev/null 2>&1; then
    dnf install -y ffmpeg || true
  elif command -v pacman >/dev/null 2>&1; then
    pacman -Sy --noconfirm ffmpeg || true
  fi
else
  echo "--> FFmpeg detected: $(command -v ffmpeg)"
fi

# 6. Restore preserved settings.json
if [ -n "${SETTINGS_BACKUP}" ] && [ -f "${SETTINGS_BACKUP}" ]; then
  echo "--> Restoring preserved settings.json..."
  cp "${SETTINGS_BACKUP}" "${SETTINGS_FILE}"
  rm -f "${SETTINGS_BACKUP}"
  chmod 666 "${SETTINGS_FILE}" 2>/dev/null || true
fi

# 7. Create symlink in /usr/local/bin
echo "--> Creating symlink in /usr/local/bin..."
ln -sf "${INSTALL_DIR}/LocalLLMServerManager" "${BIN_LINK}"

# 8. Install Desktop launcher if file exists
if [ -f "${SCRIPT_DIR}/localllmmanager.desktop" ]; then
  echo "--> Installing Desktop launcher..."
  cp "${SCRIPT_DIR}/localllmmanager.desktop" "${DESKTOP_FILE}"
  chmod 644 "${DESKTOP_FILE}"
fi

# 9. Install systemd service unit
if [ -f "${SCRIPT_DIR}/localllmmanager.service" ]; then
  echo "--> Installing systemd service..."
  cp "${SCRIPT_DIR}/localllmmanager.service" "${SERVICE_FILE}"
  chmod 644 "${SERVICE_FILE}"
else
  echo "--> Creating systemd service unit..."
  cat << 'EOF' > "${SERVICE_FILE}"
[Unit]
Description=Local LLM Server Manager
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/localllmmanager --service
Restart=always
RestartSec=5
User=root
WorkingDirectory=/usr/local/share/LocalLLMServerManager

[Install]
WantedBy=multi-user.target
EOF
  chmod 644 "${SERVICE_FILE}"
fi

# 10. Configure Firewall for Port 5246
echo "--> Configuring firewall for port 5246..."
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "Status: active"; then
  ufw allow 5246/tcp comment 'LocalLLM Server Manager' || true
  echo "--> UFW port 5246/tcp allowed."
elif command -v firewall-cmd >/dev/null 2>&1 && systemctl is-active --quiet firewalld 2>/dev/null; then
  firewall-cmd --add-port=5246/tcp --permanent || true
  firewall-cmd --reload || true
  echo "--> firewalld port 5246/tcp allowed."
fi

# 11. Optional Feature Pack Installation
if [ "${WITH_VIDEO}" -eq 1 ]; then
  echo "--> Installing Video Generation Feature Pack..."
  mkdir -p "${INSTALL_DIR}/Workflows/Video"
fi

if [ "${WITH_AUDIO}" -eq 1 ]; then
  echo "--> Installing Audio & Kokoro TTS Feature Pack..."
  mkdir -p "${INSTALL_DIR}/kokoro-fastapi"
  mkdir -p "${INSTALL_DIR}/models/audio"
  if command -v python3 >/dev/null 2>&1; then
    echo "--> Installing Python audio packages (kokoro-onnx, soundfile, fastapi, uvicorn, openai)..."
    python3 -m pip install --break-system-packages kokoro-onnx soundfile fastapi uvicorn openai || python3 -m pip install kokoro-onnx soundfile fastapi uvicorn openai || true
  fi
fi

# 12. Reload systemd daemon and restart service if it was previously running
echo "--> Reloading systemd daemon..."
systemctl daemon-reload

if [ "${SERVICE_WAS_ACTIVE}" -eq 1 ] || systemctl is-enabled --quiet "${SERVICE_NAME}" 2>/dev/null; then
  echo "--> Starting/Restarting ${SERVICE_NAME}..."
  systemctl restart "${SERVICE_NAME}" || true
fi

# 13. Summary
HOST_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
[ -z "${HOST_IP}" ] && HOST_IP="10.0.0.21"

echo ""
echo "=========================================="
echo "  Installation Complete!"
echo "=========================================="
echo "Local Dashboard:    http://localhost:5246"
echo "Local MCP Endpoint: http://localhost:5246/mcp"
echo "Network Dashboard:  http://${HOST_IP}:5246"
echo "Network MCP:        http://${HOST_IP}:5246/mcp"
echo "To start native desktop app: localllmmanager"
echo "To enable background service: sudo systemctl enable --now localllmmanager"
echo "=========================================="
