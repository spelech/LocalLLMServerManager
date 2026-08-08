import os
from huggingface_hub import hf_hub_download

checkpoints_dir = r"D:\AI\models\checkpoints"
os.makedirs(checkpoints_dir, exist_ok=True)

print("Downloading Juggernaut XL v9...")
try:
    file_path = hf_hub_download(
        repo_id="RunDiffusion/Juggernaut-XL-v9",
        filename="Juggernaut-XL_v9_RunDiffusionPhoto_v2.safetensors",
        local_dir=checkpoints_dir,
        local_dir_use_symlinks=False
    )
    print(f"Successfully downloaded to {file_path}")
except Exception as e:
    print(f"Failed to download Juggernaut XL v9: {e}")
