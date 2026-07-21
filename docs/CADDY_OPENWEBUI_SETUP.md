# Linux Caddy Proxy & Open WebUI / LibreChat Integration Guide

This guide explains how to expose **LocalLLMServerManager** (running on Windows) through a **Linux Caddy Reverse Proxy** so client applications like **Open WebUI** and **LibreChat** can access LLM inference, Stable Diffusion image generation, ComfyUI workflows, and 3D mesh endpoints securely.

---

## 🌐 Network Architecture

```
[ Open WebUI / LibreChat / Web Browser ]
                  │
                  ▼ (HTTPS / TLS)
    [ Linux Server (Caddy Reverse Proxy) ]
                  │
                  ▼ (LAN / Tailscale / WireGuard)
[ Windows Host (LocalLLMServerManager :5246) ]
   ├── Ollama LLM (:11434)
   ├── Stable Diffusion / Forge (:7860)
   └── ComfyUI 3D Engine (:8188)
```

---

## 🔒 Caddyfile Configuration (Linux Server)

On your Linux server, create or update `/etc/caddy/Caddyfile`:

```caddy
ai.yourdomain.com {
    # Dashboard & Unified Reverse Proxy
    reverse_proxy 192.168.1.100:5246 {
        header_up Host {host}
        header_up X-Real-IP {remote_host}
    }

    # Ollama API Endpoints for Open WebUI / LibreChat
    handle /v1/chat/* {
        reverse_proxy 192.168.1.100:5246
    }
    handle /v1/models/* {
        reverse_proxy 192.168.1.100:5246
    }
    handle /api/* {
        reverse_proxy 192.168.1.100:5246
    }

    # Stable Diffusion / Forge API Endpoints
    handle /sdapi/* {
        reverse_proxy 192.168.1.100:5246
    }
    handle /v1/images/* {
        reverse_proxy 192.168.1.100:5246
    }

    # ComfyUI API & WebSocket Endpoints
    handle /comfyapi/* {
        reverse_proxy 192.168.1.100:5246
    }

    # 3D Mesh Output Endpoints
    handle /3d_outputs/* {
        reverse_proxy 192.168.1.100:5246
    }
}
```

*Replace `192.168.1.100` with the LAN IP or Tailscale IP of your Windows PC.*

Reload Caddy:
```bash
sudo systemctl reload caddy
```

---

## 🤖 Open WebUI Connection Setup

To connect **Open WebUI** to `LocalLLMServerManager`:

1. Log in to Open WebUI.
2. Go to **Admin Panel ➔ Settings ➔ Connections**.
3. Under **Ollama API Base URL**, set:
   ```
   https://ai.yourdomain.com
   ```
   *(or `http://192.168.1.100:5246` if accessing over LAN directly)*.
4. Click **Save / Verify Connection**. Open WebUI will discover all installed Ollama LLMs automatically.

---

## 💬 LibreChat Connection Setup

To connect **LibreChat** to `LocalLLMServerManager`:

Update your `librechat.yaml` configuration:

```yaml
endpoints:
  custom:
    - name: "Local LLM Server"
      apiKey: "user-provided"
      baseURL: "https://ai.yourdomain.com/v1"
      models:
        default: ["llama3.2", "qwen2.5-coder"]
        fetch: true
      titleConvo: true
      modelDisplayLabel: "Local GPU Server"
```

Restart LibreChat to apply changes.
