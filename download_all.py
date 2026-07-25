import urllib.request
import os

downloads = [
    {
        "url": "https://huggingface.co/dvyio/flux-lora-spongebob/resolve/main/spongebob.safetensors",
        "dest": r"D:\AI\comfy_models\loras\spongebob.safetensors"
    },
    {
        "url": "https://huggingface.co/Lykon/dreamshaper-xl-1-0/resolve/main/DreamShaperXL1.0_alpha2.safetensors",
        "dest": r"D:\AI\models\checkpoints\DreamShaperXL.safetensors"
    }
]

for d in downloads:
    dest_dir = os.path.dirname(d["dest"])
    os.makedirs(dest_dir, exist_ok=True)
    print(f"Downloading {d['url']} to {d['dest']}...")
    try:
        urllib.request.urlretrieve(d["url"], d["dest"])
        print(f"Successfully downloaded {d['dest']}")
    except Exception as e:
        print(f"Failed to download {d['dest']}: {e}")
