# Script to download Video & 3D base models for ComfyUI

$modelsDir = "D:\AI\models"
$checkpointsDir = Join-Path $modelsDir "checkpoints"
$animateDiffDir = Join-Path $modelsDir "animatediff_models"

# Ensure directories exist
if (-not (Test-Path $checkpointsDir)) { New-Item -ItemType Directory -Path $checkpointsDir -Force }
if (-not (Test-Path $animateDiffDir)) { New-Item -ItemType Directory -Path $animateDiffDir -Force }

# 1. AnimateDiff SDXL Motion Module
# Allows you to animate the SDXL models you already downloaded (like Juggernaut X and Pony V6 XL)
$adSdxlUrl = "https://huggingface.co/guoyww/animatediff/resolve/main/mm_sdxl_v10_beta.ckpt"
$adSdxlDest = Join-Path $animateDiffDir "mm_sdxl_v10_beta.ckpt"

if (-not (Test-Path $adSdxlDest)) {
    Write-Host "Downloading AnimateDiff SDXL Motion Module (~1.8 GB)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $adSdxlUrl -OutFile $adSdxlDest
} else {
    Write-Host "AnimateDiff SDXL already exists." -ForegroundColor Yellow
}

# 2. Stable Video Diffusion (SVD) XT 1.1
# Excellent Image-to-Video generation model
$svdUrl = "https://huggingface.co/stabilityai/stable-video-diffusion-img2vid-xt-1-1/resolve/main/svd_xt_1_1.safetensors"
$svdDest = Join-Path $checkpointsDir "svd_xt_1_1.safetensors"

if (-not (Test-Path $svdDest)) {
    Write-Host "Downloading Stable Video Diffusion XT 1.1 (~9.5 GB)..." -ForegroundColor Cyan
    # Read HF_TOKEN from .env file
    $envPath = Join-Path $PSScriptRoot ".env"
    $hfToken = ""
    if (Test-Path $envPath) {
        $envLines = Get-Content $envPath
        foreach ($line in $envLines) {
            if ($line.StartsWith("HF_TOKEN=")) {
                $hfToken = $line.Substring(9).Trim()
            }
        }
    }
    
    if (-not $hfToken) {
        Write-Host "WARNING: HF_TOKEN not found in .env. Attempting download without auth..." -ForegroundColor Yellow
    }

    $headers = @{}
    if ($hfToken) {
        $headers["Authorization"] = "Bearer $hfToken"
    }
    Invoke-WebRequest -Uri $svdUrl -OutFile $svdDest -Headers $headers
} else {
    Write-Host "Stable Video Diffusion already exists." -ForegroundColor Yellow
}

# Note on 3D Models:
# TRELLIS V2 and Hunyuan3D v2 weights are extremely fragmented (multi-part huggingface repos).
# The custom nodes (ComfyUI-Trellis and ComfyUI-Hunyuan3DWrapper) are designed to automatically 
# download their required weights upon the first generation attempt using the python huggingface_hub library.
# We will let the nodes handle their own 3D weight downloads to prevent missing files!

Write-Host "Video model downloads complete! 3D models will auto-download on first use." -ForegroundColor Green
