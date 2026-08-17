[CmdletBinding()]
param(
    [string]$ModelsDir = "",
    [string]$SettingsJson = "",
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

# Ensure the directory exists
if (-not (Test-Path $checkpointsDir)) {
    New-Item -ItemType Directory -Path $checkpointsDir -Force | Out-Null
}

Write-Host "Target Checkpoints Directory: $checkpointsDir" -ForegroundColor Cyan

# 1. Juggernaut X (Latest highly realistic SDXL model)
$juggernautUrl = "https://huggingface.co/RunDiffusion/Juggernaut-X-v10/resolve/main/Juggernaut-X-RunDiffusion-fp16.safetensors"
$juggernautDest = Join-Path $checkpointsDir "Juggernaut-X.safetensors"

if (-not (Test-Path $juggernautDest)) {
    Write-Host "Downloading Juggernaut X (~6.5 GB)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $juggernautUrl -OutFile $juggernautDest
} else {
    Write-Host "Juggernaut X already exists at $juggernautDest." -ForegroundColor Yellow
}

# 2. Pony Diffusion V6 XL (The absolute gold standard for explicit/NSFW and stylized content)
$ponyUrl = "https://huggingface.co/ponybot/ponyDiffusionV6XL/resolve/main/ponyDiffusionV6XL_v6StartWithThisOne.safetensors"
$ponyDest = Join-Path $checkpointsDir "PonyDiffusionV6XL.safetensors"

if (-not (Test-Path $ponyDest)) {
    Write-Host "Downloading Pony Diffusion V6 XL (~6.5 GB)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $ponyUrl -OutFile $ponyDest
} else {
    Write-Host "Pony Diffusion V6 XL already exists at $ponyDest." -ForegroundColor Yellow
}

Write-Host "Starter model downloads complete!" -ForegroundColor Green
