# Caddy Remote Setup for AI Services

This document contains the Caddyfile configuration needed on the remote proxy machine to route traffic to the AI services on this local PC (`10.0.0.21`).

## Services and Ports
- **ComfyUI**: `10.0.0.21:8188`
- **Stable Diffusion Forge**: `10.0.0.21:7860`
- **Ollama**: `10.0.0.21:11434`

## Caddyfile Configuration (using Tinyauth)

Pass this Caddyfile block to the agent managing your remote Caddy server. Ensure you replace `yourdomain.com` with your actual domain, and verify the `tinyauth.sock` path for your environment.

```caddyfile
# ComfyUI
comfy.yourdomain.com {
    forward_auth * unix//var/run/tinyauth.sock {
        uri /auth
    }
    reverse_proxy 10.0.0.21:8188
}

# Stable Diffusion WebUI Forge
forge.yourdomain.com {
    forward_auth * unix//var/run/tinyauth.sock {
        uri /auth
    }
    reverse_proxy 10.0.0.21:7860
}

# Ollama API
ollama.yourdomain.com {
    forward_auth * unix//var/run/tinyauth.sock {
        uri /auth
    }
    reverse_proxy 10.0.0.21:11434
}
```
