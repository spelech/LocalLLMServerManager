[CmdletBinding()]
param(
    [string]$ModelsDir = "",
    [string]$SettingsJson = "",
    [string]$HfToken = "",
    [switch]$Interactive
)

function Get-AppSettings {
    param([string]$Path)
    
    $candidates = @()
    if ($Path) { $candidates += $Path }
    $candidates += (Join-Path $PSScriptRoot "settings.json")
    $candidates += (Join-Path $PSScriptRoot "..\settings.json")
    if ($env:APPDATA) {
        $candidates += (Join-Path $env:APPDATA "LocalLLMServerManager\settings.json")
    }
    
    foreach ($cand in $candidates) {
        if ($cand -and (Test-Path $cand)) {
            try {
                $raw = Get-Content -Raw -Path $cand -ErrorAction Stop
                return ($raw | ConvertFrom-Json)
            } catch {
                Write-Verbose "Could not parse JSON from $($cand): $_"
            }
        }
    }
    return $null
}

function Resolve-PathVariables {
    param([string]$Path)
    if (-not $Path) { return "" }
    return [System.Environment]::ExpandEnvironmentVariables($Path)
}

function Get-HfToken {
    param([string]$Token)
    if ($Token) { return $Token }
    if ($env:HF_TOKEN) { return $env:HF_TOKEN }
    
    $envPaths = @(
        (Join-Path $PSScriptRoot ".env"),
        (Join-Path (Get-Location) ".env"),
        (Join-Path $PSScriptRoot "..\.env")
    )
    foreach ($envPath in $envPaths) {
        if (Test-Path $envPath) {
            $lines = Get-Content $envPath
            foreach ($line in $lines) {
                if ($line.StartsWith("HF_TOKEN=")) {
                    return $line.Substring(9).Trim().Trim('"').Trim("'")
                }
            }
        }
    }
    return ""
}

# Resolve ModelsDir
if (-not $ModelsDir) {
    $settings = Get-AppSettings -Path $SettingsJson
    if ($settings) {
        if ($settings.ForgeModelsPath) {
            $ModelsDir = Resolve-PathVariables $settings.ForgeModelsPath
        } elseif ($settings.ComfyModelsPath) {
            $ModelsDir = Resolve-PathVariables $settings.ComfyModelsPath
        }
    }
}

if (-not $ModelsDir -and $Interactive) {
    $defaultDir = Join-Path $env:USERPROFILE "AI\models"
    $inputDir = Read-Host "Enter models directory [Default: $defaultDir]"
    if ($inputDir) {
        $ModelsDir = $inputDir
    } else {
        $ModelsDir = $defaultDir
    }
}

if (-not $ModelsDir) {
    $ModelsDir = Join-Path $env:USERPROFILE "AI\models"
}

$checkpointsDir = Join-Path $ModelsDir "checkpoints"
$animateDiffDir = Join-Path $ModelsDir "animatediff_models"

# Ensure directories exist
if (-not (Test-Path $checkpointsDir)) { New-Item -ItemType Directory -Path $checkpointsDir -Force | Out-Null }
if (-not (Test-Path $animateDiffDir)) { New-Item -ItemType Directory -Path $animateDiffDir -Force | Out-Null }

Write-Host "Target Models Directory: $ModelsDir" -ForegroundColor Cyan

# 1. AnimateDiff SDXL Motion Module
# Allows you to animate the SDXL models you already downloaded (like Juggernaut X and Pony V6 XL)
$adSdxlUrl = "https://huggingface.co/guoyww/animatediff/resolve/main/mm_sdxl_v10_beta.ckpt"
$adSdxlDest = Join-Path $animateDiffDir "mm_sdxl_v10_beta.ckpt"

if (-not (Test-Path $adSdxlDest)) {
    Write-Host "Downloading AnimateDiff SDXL Motion Module (~1.8 GB)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $adSdxlUrl -OutFile $adSdxlDest
} else {
    Write-Host "AnimateDiff SDXL already exists at $adSdxlDest." -ForegroundColor Yellow
}

# 2. Stable Video Diffusion (SVD) XT 1.1
# Excellent Image-to-Video generation model
$svdUrl = "https://huggingface.co/stabilityai/stable-video-diffusion-img2vid-xt-1-1/resolve/main/svd_xt_1_1.safetensors"
$svdDest = Join-Path $checkpointsDir "svd_xt_1_1.safetensors"

if (-not (Test-Path $svdDest)) {
    Write-Host "Downloading Stable Video Diffusion XT 1.1 (~9.5 GB)..." -ForegroundColor Cyan
    $resolvedToken = Get-HfToken -Token $HfToken
    
    if (-not $resolvedToken) {
        Write-Host "WARNING: HF_TOKEN not found. Attempting download without auth..." -ForegroundColor Yellow
    }

    $headers = @{}
    if ($resolvedToken) {
        $headers["Authorization"] = "Bearer $resolvedToken"
    }
    Invoke-WebRequest -Uri $svdUrl -OutFile $svdDest -Headers $headers
} else {
    Write-Host "Stable Video Diffusion already exists at $svdDest." -ForegroundColor Yellow
}

# Note on 3D Models:
# TRELLIS V2 and Hunyuan3D v2 weights are extremely fragmented (multi-part huggingface repos).
# The custom nodes (ComfyUI-Trellis and ComfyUI-Hunyuan3DWrapper) are designed to automatically 
# download their required weights upon the first generation attempt using the python huggingface_hub library.
# We will let the nodes handle their own 3D weight downloads to prevent missing files!

Write-Host "Video model downloads complete! 3D models will auto-download on first use." -ForegroundColor Green
