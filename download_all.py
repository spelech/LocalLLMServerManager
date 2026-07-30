import urllib.request
import os

downloads = [
    {
        "url": "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/ae.safetensors",
        "dest": r"D:\AI\ComfyUI\models\vae\ae.safetensors"
    },
    {
        "url": "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/flux1-dev.safetensors",
        "dest": r"D:\AI\ComfyUI\models\unet\flux1-dev.safetensors"
    }
]

def get_hf_token():
    env_path = os.path.join(os.path.dirname(__file__), '.env')
    if os.path.exists(env_path):
        with open(env_path, 'r') as f:
            for line in f:
                if line.startswith('HF_TOKEN='):
                    return line.strip().split('=', 1)[1]
    return None

hf_token = get_hf_token()

for d in downloads:
    dest_dir = os.path.dirname(d["dest"])
    os.makedirs(dest_dir, exist_ok=True)
    print(f"Downloading {d['url']} to {d['dest']}...")
    try:
        req = urllib.request.Request(d["url"])
        if hf_token:
            req.add_header("Authorization", f"Bearer {hf_token}")
        with urllib.request.urlopen(req) as response, open(d["dest"], 'wb') as out_file:
            out_file.write(response.read())
        print(f"Successfully downloaded {d['dest']}")
    except Exception as e:
        print(f"Failed to download {d['dest']}: {e}")
