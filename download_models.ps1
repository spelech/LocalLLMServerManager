# Script to download starter models for ComfyUI / SD Forge

$checkpointsDir = "D:\AI\models\checkpoints"

# Ensure the directory exists
if (-not (Test-Path $checkpointsDir)) {
    New-Item -ItemType Directory -Path $checkpointsDir -Force
}

# 1. Juggernaut X (Latest highly realistic SDXL model)
$juggernautUrl = "https://huggingface.co/RunDiffusion/Juggernaut-X-v10/resolve/main/Juggernaut-X-RunDiffusion-fp16.safetensors"
$juggernautDest = Join-Path $checkpointsDir "Juggernaut-X.safetensors"

Write-Host "Downloading Juggernaut X (~6.5 GB)..."
Invoke-WebRequest -Uri $juggernautUrl -OutFile $juggernautDest

# 2. Pony Diffusion V6 XL (The absolute gold standard for explicit/NSFW and stylized content)
$ponyUrl = "https://huggingface.co/ponybot/ponyDiffusionV6XL/resolve/main/ponyDiffusionV6XL_v6StartWithThisOne.safetensors"
$ponyDest = Join-Path $checkpointsDir "PonyDiffusionV6XL.safetensors"

Write-Host "Downloading Pony Diffusion V6 XL (~6.5 GB)..."
Invoke-WebRequest -Uri $ponyUrl -OutFile $ponyDest

Write-Host "Starter model downloads complete!"
