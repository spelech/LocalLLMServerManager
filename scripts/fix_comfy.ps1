[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$ModelsDir = "",
    [string]$ComfyUiPath = "",
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

$settings = Get-AppSettings -Path $SettingsJson

# Resolve TargetDir
if (-not $TargetDir) {
    if ($ComfyUiPath) {
        $TargetDir = Split-Path -Path $ComfyUiPath -Parent
    } elseif ($settings -and $settings.ComfyUiExecutablePath) {
        $exe = Resolve-PathVariables $settings.ComfyUiExecutablePath
        $parent = Split-Path -Path $exe -Parent
        $grandParent = Split-Path -Path $parent -Parent
        if ($grandParent -and (Test-Path $grandParent)) {
            $TargetDir = $grandParent
        } elseif ($parent -and (Test-Path $parent)) {
            $TargetDir = $parent
        }
    }
}

if (-not $TargetDir -and $Interactive) {
    $defaultDir = Join-Path $env:USERPROFILE "AI"
    $inputDir = Read-Host "Enter AI tools base directory [Default: $defaultDir]"
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

# Resolve ModelsDir
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

# Rename legacy portable folders if needed
$comfyPortable = Join-Path $TargetDir "ComfyUI_windows_portable"
$comfyFolder = Join-Path $TargetDir "ComfyUI"
if (Test-Path $comfyPortable) {
    if (Test-Path $comfyFolder) { Remove-Item $comfyFolder -Recurse -Force }
    Rename-Item $comfyPortable -NewName "ComfyUI"
}

$webUiFolder = Join-Path $TargetDir "WebUI"
$forgeFolder = Join-Path $TargetDir "SD_Forge"
if (Test-Path $webUiFolder) {
    if (Test-Path $forgeFolder) { Remove-Item $forgeFolder -Recurse -Force }
    Rename-Item $webUiFolder -NewName "SD_Forge"
}

# Locate ComfyUI directory
if (-not $ComfyUiPath) {
    if (Test-Path $comfyFolder) {
        $ComfyUiPath = $comfyFolder
    } else {
        $ComfyUiPath = $TargetDir
    }
}

# Configure extra_model_paths.yaml
$extraModelPaths = @"
comfyui:
    base_path: $ModelsDir
    checkpoints: checkpoints
    loras: loras
    vae: vae
    controlnet: controlnet
"@

$yamlPaths = @(
    (Join-Path $ComfyUiPath "ComfyUI\extra_model_paths.yaml"),
    (Join-Path $ComfyUiPath "extra_model_paths.yaml")
)
foreach ($yamlPath in $yamlPaths) {
    $parent = Split-Path -Path $yamlPath -Parent
    if (Test-Path $parent) {
        $extraModelPaths | Out-File -FilePath $yamlPath -Encoding utf8
        Write-Host "Configured extra_model_paths.yaml at $yamlPath" -ForegroundColor Green
    }
}

# ComfyUI-Manager
$managerPaths = @(
    (Join-Path $ComfyUiPath "ComfyUI\custom_nodes\ComfyUI-Manager"),
    (Join-Path $ComfyUiPath "custom_nodes\ComfyUI-Manager")
)
$managerInstalled = $false
foreach ($mgr in $managerPaths) {
    if (Test-Path $mgr) {
        $managerInstalled = $true
        break
    }
}

if (-not $managerInstalled) {
    $destCustomNodes = Join-Path $ComfyUiPath "ComfyUI\custom_nodes"
    if (-not (Test-Path $destCustomNodes)) {
        $destCustomNodes = Join-Path $ComfyUiPath "custom_nodes"
    }
    if (-not (Test-Path $destCustomNodes)) {
        New-Item -ItemType Directory -Path $destCustomNodes -Force | Out-Null
    }
    git clone https://github.com/ltdrdata/ComfyUI-Manager.git (Join-Path $destCustomNodes "ComfyUI-Manager")
}

# Run install_comfy_nodes.ps1
$installScript = Join-Path $PSScriptRoot "install_comfy_nodes.ps1"
if (Test-Path $installScript) {
    & $installScript -ComfyUiPath $ComfyUiPath -SettingsJson $SettingsJson
}
