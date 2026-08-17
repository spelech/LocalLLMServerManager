import argparse
import json
import os
import sys
from huggingface_hub import hf_hub_download

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
        description="Download SDXL checkpoint models from Hugging Face Hub."
    )
    parser.add_argument(
        "-c", "--checkpoints-dir",
        dest="checkpoints_dir",
        default=None,
        help="Directory to save checkpoints (e.g. C:\\AI\\models\\checkpoints)"
    )
    parser.add_argument(
        "-m", "--models-dir",
        dest="models_dir",
        default=None,
        help="Base models directory (checkpoints/ will be created inside)"
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

    checkpoints_dir = args.checkpoints_dir
    if not checkpoints_dir:
        if args.models_dir:
            checkpoints_dir = os.path.join(args.models_dir, "checkpoints")
        else:
            settings = load_settings(args.settings_json)
            if settings:
                if settings.get("ForgeModelsPath"):
                    base = resolve_env_path(settings["ForgeModelsPath"])
                    checkpoints_dir = os.path.join(base, "checkpoints") if not base.endswith("checkpoints") else base
                elif settings.get("ComfyModelsPath"):
                    base = resolve_env_path(settings["ComfyModelsPath"])
                    checkpoints_dir = os.path.join(base, "checkpoints") if not base.endswith("checkpoints") else base

    if not checkpoints_dir:
        fallback = os.path.expanduser(os.path.join("~", "AI", "models", "checkpoints"))
        print(f"No checkpoint directory specified. Using default fallback: {fallback}")
        checkpoints_dir = fallback

    checkpoints_dir = os.path.abspath(checkpoints_dir)
    os.makedirs(checkpoints_dir, exist_ok=True)
    print(f"Target Checkpoints Directory: {checkpoints_dir}")

    token = get_hf_token(args.token)

    models = [
        {
            "repo_id": "RunDiffusion/Juggernaut-X-v10",
            "filename": "Juggernaut-X-RunDiffusion-fp16.safetensors"
        },
        {
            "repo_id": "LyliaEngine/Pony_Diffusion_V6_XL",
            "filename": "ponyDiffusionV6XL_v6StartWithThisOne.safetensors"
        }
    ]

    for m in models:
        dest_file = os.path.join(checkpoints_dir, m["filename"])
        if os.path.exists(dest_file):
            print(f"Model already exists: {dest_file}")
            continue

        print(f"Downloading {m['filename']} from {m['repo_id']}...")
        try:
            file_path = hf_hub_download(
                repo_id=m["repo_id"],
                filename=m["filename"],
                local_dir=checkpoints_dir,
                local_dir_use_symlinks=False,
                token=token
            )
            print(f"Successfully downloaded to {file_path}")
        except Exception as e:
            print(f"Failed to download {m['filename']}: {e}", file=sys.stderr)
            if "Juggernaut" in m["filename"]:
                try:
                    print("Trying alternative filename Juggernaut-X-v10.safetensors...")
                    file_path = hf_hub_download(
                        repo_id=m["repo_id"],
                        filename="Juggernaut-X-v10.safetensors",
                        local_dir=checkpoints_dir,
                        local_dir_use_symlinks=False,
                        token=token
                    )
                    print(f"Successfully downloaded to {file_path}")
                except Exception as e2:
                    print(f"Also failed alternative: {e2}", file=sys.stderr)
            elif "Pony" in m["filename"]:
                try:
                    print("Trying alternative filename ponyDiffusionV6XL.safetensors...")
                    file_path = hf_hub_download(
                        repo_id=m["repo_id"],
                        filename="ponyDiffusionV6XL.safetensors",
                        local_dir=checkpoints_dir,
                        local_dir_use_symlinks=False,
                        token=token
                    )
                    print(f"Successfully downloaded to {file_path}")
                except Exception as e2:
                    print(f"Also failed alternative: {e2}", file=sys.stderr)

if __name__ == "__main__":
    main()
