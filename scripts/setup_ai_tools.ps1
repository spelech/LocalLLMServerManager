[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$ModelsDir = "",
    [string]$SettingsJson = "",
    [string]$SevenZipPath = "",
    [switch]$Interactive,
    [switch]$InstallVideoPack,
    [switch]$InstallAudioPack
)

function Install-VideoPack {
    param([string]$TargetDir, [string]$ModelsDir)
    Write-Host "Executing Install-VideoPack..." -ForegroundColor Cyan
    $videoDir = Join-Path $TargetDir "Workflows\Video"
    $diffDir = Join-Path $ModelsDir "diffusion_models"
    if (-not (Test-Path $videoDir)) { New-Item -ItemType Directory -Path $videoDir -Force | Out-Null }
    if (-not (Test-Path $diffDir)) { New-Item -ItemType Directory -Path $diffDir -Force | Out-Null }
    Write-Host "Video Feature Pack installed." -ForegroundColor Green
}

function Install-AudioPack {
    param([string]$TargetDir, [string]$ModelsDir)
    Write-Host "Executing Install-AudioPack..." -ForegroundColor Cyan
    $kokoroDir = Join-Path $TargetDir "kokoro-fastapi"
    $audioDir = Join-Path $ModelsDir "audio"
    if (-not (Test-Path $kokoroDir)) { New-Item -ItemType Directory -Path $kokoroDir -Force | Out-Null }
    if (-not (Test-Path $audioDir)) { New-Item -ItemType Directory -Path $audioDir -Force | Out-Null }
    Write-Host "Audio Feature Pack installed." -ForegroundColor Green
}

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

# 1. Resolve TargetDir (where ComfyUI and SD_Forge portable folders reside)
$settings = Get-AppSettings -Path $SettingsJson

if (-not $TargetDir) {
    if ($settings) {
        if ($settings.ComfyUiExecutablePath) {
            $exe = Resolve-PathVariables $settings.ComfyUiExecutablePath
            $parent = Split-Path -Path $exe -Parent
            $grandParent = Split-Path -Path $parent -Parent
            if ($grandParent -and (Test-Path $grandParent)) {
                $TargetDir = $grandParent
            } elseif ($parent -and (Test-Path $parent)) {
                $TargetDir = $parent
            }
        } elseif ($settings.ForgeExecutablePath) {
            $exe = Resolve-PathVariables $settings.ForgeExecutablePath
            $parent = Split-Path -Path $exe -Parent
            $grandParent = Split-Path -Path $parent -Parent
            if ($grandParent -and (Test-Path $grandParent)) {
                $TargetDir = $grandParent
            } elseif ($parent -and (Test-Path $parent)) {
                $TargetDir = $parent
            }
        }
    }
}

if (-not $TargetDir -and $Interactive) {
    $defaultDir = Join-Path $env:USERPROFILE "AI"
    $inputDir = Read-Host "Enter target installation directory for AI tools [Default: $defaultDir]"
    if ($inputDir) {
        $TargetDir = $inputDir
    } else {
        $TargetDir = $defaultDir
    }
}

if (-not $TargetDir) {
    $candidates = @(
        (Join-Path $env:USERPROFILE "AI"),
        (Join-Path $env:SystemDrive "AI")
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $TargetDir = $cand
            break
        }
    }
    if (-not $TargetDir) {
        $TargetDir = Join-Path $env:USERPROFILE "AI"
    }
}

# 2. Resolve ModelsDir
if (-not $ModelsDir) {
    if ($settings) {
        if ($settings.ForgeModelsPath) {
            $ModelsDir = Resolve-PathVariables $settings.ForgeModelsPath
        } elseif ($settings.ComfyModelsPath) {
            $ModelsDir = Resolve-PathVariables $settings.ComfyModelsPath
        }
    }
}
if (-not $ModelsDir) {
    $ModelsDir = Join-Path $TargetDir "models"
}

# Ensure base directories exist
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}
if (-not (Test-Path $ModelsDir)) {
    New-Item -ItemType Directory -Path $ModelsDir -Force | Out-Null
}

# 3. Locate 7-Zip
if (-not $SevenZipPath) {
    $sevenZipCmd = Get-Command 7z -ErrorAction SilentlyContinue
    if ($sevenZipCmd) {
        $SevenZipPath = $sevenZipCmd.Source
    } else {
        $szCandidates = @(
            "C:\Program Files\7-Zip\7z.exe",
            "C:\Program Files (x86)\7-Zip\7z.exe",
            (Join-Path $env:ProgramFiles "7-Zip\7z.exe")
        )
        foreach ($sz in $szCandidates) {
            if ($sz -and (Test-Path $sz)) {
                $SevenZipPath = $sz
                break
            }
        }
    }
}

if (-not $SevenZipPath -or -not (Test-Path $SevenZipPath)) {
    Write-Host "Error: 7-Zip executable not found. Please install 7-Zip or pass -SevenZipPath." -ForegroundColor Red
    exit 1
}

Write-Host "Target Directory: $TargetDir" -ForegroundColor Cyan
Write-Host "Models Directory: $ModelsDir" -ForegroundColor Cyan
Write-Host "7-Zip Executable: $SevenZipPath" -ForegroundColor Cyan

$comfyUrl = "https://github.com/comfyanonymous/ComfyUI/releases/latest/download/ComfyUI_windows_portable_nvidia.7z"
$comfyZip = Join-Path $TargetDir "ComfyUI_windows_portable.7z"

$forgeUrl = "https://github.com/lllyasviel/stable-diffusion-webui-forge/releases/latest/download/WebUI_Forge_cu121_torch231.7z"
$forgeZip = Join-Path $TargetDir "WebUI_Forge.7z"

Write-Host "Downloading ComfyUI Portable..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $comfyUrl -OutFile $comfyZip

Write-Host "Extracting ComfyUI to $TargetDir..." -ForegroundColor Cyan
& $SevenZipPath x $comfyZip "-o$TargetDir" -y

$comfyPortable = Join-Path $TargetDir "ComfyUI_windows_portable"
$comfyDir = Join-Path $TargetDir "ComfyUI"
if (Test-Path $comfyPortable) {
    Rename-Item -Path $comfyPortable -NewName "ComfyUI" -ErrorAction SilentlyContinue
}

Write-Host "Downloading SD WebUI Forge..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $forgeUrl -OutFile $forgeZip

Write-Host "Extracting SD Forge to $TargetDir..." -ForegroundColor Cyan
& $SevenZipPath x $forgeZip "-o$TargetDir" -y

$forgeExtracted = Join-Path $TargetDir "WebUI"
$forgeDir = Join-Path $TargetDir "SD_Forge"
if (Test-Path $forgeExtracted) {
    Rename-Item -Path $forgeExtracted -NewName "SD_Forge" -ErrorAction SilentlyContinue
}

Write-Host "Cleaning up archives..." -ForegroundColor Cyan
if (Test-Path $comfyZip) { Remove-Item $comfyZip -Force -ErrorAction SilentlyContinue }
if (Test-Path $forgeZip) { Remove-Item $forgeZip -Force -ErrorAction SilentlyContinue }

Write-Host "Configuring ComfyUI to use shared models at $ModelsDir..." -ForegroundColor Cyan
$extraModelPaths = @"
comfyui:
    base_path: $ModelsDir
    checkpoints: checkpoints
    loras: loras
    vae: vae
    controlnet: controlnet
"@

$yamlPaths = @(
    (Join-Path $comfyDir "ComfyUI\extra_model_paths.yaml"),
    (Join-Path $comfyDir "extra_model_paths.yaml")
)
foreach ($yamlPath in $yamlPaths) {
    $parent = Split-Path -Path $yamlPath -Parent
    if (Test-Path $parent) {
        $extraModelPaths | Out-File -FilePath $yamlPath -Encoding utf8
        Write-Host "Saved extra_model_paths.yaml to $yamlPath" -ForegroundColor Green
    }
}

Write-Host "Installing ComfyUI Manager..." -ForegroundColor Cyan
$customNodesDir = Join-Path $comfyDir "ComfyUI\custom_nodes"
if (-not (Test-Path $customNodesDir)) {
    $customNodesDir = Join-Path $comfyDir "custom_nodes"
}
if (-not (Test-Path $customNodesDir)) {
    New-Item -ItemType Directory -Path $customNodesDir -Force | Out-Null
}

$managerDir = Join-Path $customNodesDir "ComfyUI-Manager"
if (-not (Test-Path $managerDir)) {
    git clone https://github.com/ltdrdata/ComfyUI-Manager.git $managerDir
} else {
    Write-Host "ComfyUI-Manager already present at $managerDir" -ForegroundColor Yellow
}

if ($InstallVideoPack) {
    Install-VideoPack -TargetDir $TargetDir -ModelsDir $ModelsDir
}

if ($InstallAudioPack) {
    Install-AudioPack -TargetDir $TargetDir -ModelsDir $ModelsDir
}

Write-Host "Setup Scripts Completed successfully!" -ForegroundColor Green
