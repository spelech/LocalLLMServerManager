import os
from huggingface_hub import hf_hub_download

checkpoints_dir = r"D:\AI\models\checkpoints"
os.makedirs(checkpoints_dir, exist_ok=True)

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
    print(f"Downloading {m['filename']} from {m['repo_id']}...")
    try:
        # Download and move to the target directory
        file_path = hf_hub_download(
            repo_id=m["repo_id"],
            filename=m["filename"],
            local_dir=checkpoints_dir,
            local_dir_use_symlinks=False
        )
        print(f"Successfully downloaded to {file_path}")
    except Exception as e:
        print(f"Failed to download {m['filename']}: {e}")
        # Try alternate common filenames if the first one fails
        if "Juggernaut" in m["filename"]:
            try:
                print("Trying alternative filename Juggernaut-X-v10.safetensors...")
                file_path = hf_hub_download(repo_id=m["repo_id"], filename="Juggernaut-X-v10.safetensors", local_dir=checkpoints_dir, local_dir_use_symlinks=False)
                print(f"Successfully downloaded to {file_path}")
            except Exception as e2:
                print(f"Also failed alternative: {e2}")
        elif "Pony" in m["filename"]:
            try:
                print("Trying alternative filename ponyDiffusionV6XL.safetensors...")
                file_path = hf_hub_download(repo_id=m["repo_id"], filename="ponyDiffusionV6XL.safetensors", local_dir=checkpoints_dir, local_dir_use_symlinks=False)
                print(f"Successfully downloaded to {file_path}")
            except Exception as e2:
                print(f"Also failed alternative: {e2}")
