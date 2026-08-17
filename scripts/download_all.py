import argparse
import json
import os
import sys
import urllib.request

def load_settings(settings_path=None):
    candidates = []
    if settings_path:
        candidates.append(settings_path)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    candidates.append(os.path.join(script_dir, "settings.json"))
    candidates.append(os.path.join(os.path.dirname(script_dir), "settings.json"))
    appdata = os.environ.get("APPDATA")
    if appdata:
        candidates.append(os.path.join(appdata, "LocalLLMServerManager", "settings.json"))

    for c in candidates:
        if c and os.path.exists(c):
            try:
                with open(c, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception as e:
                print(f"Warning: Could not parse {c}: {e}", file=sys.stderr)
    return None

def resolve_env_path(path_str):
    if not path_str:
        return ""
    return os.path.expandvars(os.path.expanduser(path_str))

def get_hf_token(token_arg=None):
    if token_arg:
        return token_arg
    if os.environ.get("HF_TOKEN"):
        return os.environ.get("HF_TOKEN")
    
    script_dir = os.path.dirname(os.path.abspath(__file__))
    env_paths = [
        os.path.join(script_dir, ".env"),
        os.path.join(os.getcwd(), ".env"),
        os.path.join(os.path.dirname(script_dir), ".env")
    ]
    for env_path in env_paths:
        if os.path.exists(env_path):
            with open(env_path, "r", encoding="utf-8") as f:
                for line in f:
                    if line.startswith("HF_TOKEN="):
                        return line.strip().split("=", 1)[1].strip('"\'')
    return None

def main():
    parser = argparse.ArgumentParser(
        description="Download FLUX.1 base models (VAE & UNET) to ComfyUI/models directory."
    )
    parser.add_argument(
        "-m", "--models-dir",
        dest="models_dir",
        default=None,
        help="Path to models directory (e.g. C:\\AI\\models or ComfyUI\\models)"
    )
    parser.add_argument(
        "-o", "--output-dir",
        dest="output_dir",
        default=None,
        help="Alias for models-dir"
    )
    parser.add_argument(
        "-s", "--settings-json",
        dest="settings_json",
        default=None,
        help="Path to settings.json file"
    )
    parser.add_argument(
        "-t", "--token",
        dest="token",
        default=None,
        help="Hugging Face API token (or read from HF_TOKEN env / .env)"
    )

    args = parser.parse_args()

    models_dir = args.models_dir or args.output_dir
    if not models_dir:
        settings = load_settings(args.settings_json)
        if settings:
            if settings.get("ComfyModelsPath"):
                models_dir = resolve_env_path(settings["ComfyModelsPath"])
            elif settings.get("ForgeModelsPath"):
                models_dir = resolve_env_path(settings["ForgeModelsPath"])

    if not models_dir:
        fallback = os.path.expanduser(os.path.join("~", "AI", "models"))
        print(f"No --models-dir or settings.json configuration found. Using default fallback: {fallback}")
        models_dir = fallback

    models_dir = os.path.abspath(models_dir)
    print(f"Target Models Directory: {models_dir}")

    downloads = [
        {
            "url": "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/ae.safetensors",
            "dest": os.path.join(models_dir, "vae", "ae.safetensors")
        },
        {
            "url": "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/flux1-dev.safetensors",
            "dest": os.path.join(models_dir, "unet", "flux1-dev.safetensors")
        }
    ]

    hf_token = get_hf_token(args.token)

    for d in downloads:
        dest_dir = os.path.dirname(d["dest"])
        os.makedirs(dest_dir, exist_ok=True)
        if os.path.exists(d["dest"]):
            print(f"File already exists: {d['dest']}")
            continue

        print(f"Downloading {d['url']} to {d['dest']}...")
        try:
            req = urllib.request.Request(d["url"])
            if hf_token:
                req.add_header("Authorization", f"Bearer {hf_token}")
            with urllib.request.urlopen(req) as response, open(d["dest"], "wb") as out_file:
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    out_file.write(chunk)
            print(f"Successfully downloaded {d['dest']}")
        except Exception as e:
            print(f"Failed to download {d['dest']}: {e}", file=sys.stderr)

if __name__ == "__main__":
    main()
