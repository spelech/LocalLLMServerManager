[CmdletBinding()]
param(
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

function Find-CustomNodesDir {
    param([string]$BasePath)
    if (-not $BasePath) { return "" }
    
    if ($BasePath -like "*custom_nodes*" -and (Test-Path $BasePath)) {
        return $BasePath
    }
    
    $sub1 = Join-Path $BasePath "ComfyUI\custom_nodes"
    if (Test-Path $sub1) { return $sub1 }
    
    $sub2 = Join-Path $BasePath "custom_nodes"
    if (Test-Path $sub2) { return $sub2 }
    
    if (Test-Path (Join-Path $BasePath "ComfyUI")) {
        return $sub1
    }
    
    return $sub2
}

# Resolve ComfyUiPath
if (-not $ComfyUiPath) {
    $settings = Get-AppSettings -Path $SettingsJson
    if ($settings -and $settings.ComfyUiExecutablePath) {
        $exe = Resolve-PathVariables $settings.ComfyUiExecutablePath
        if ($exe) {
            $parent = Split-Path -Path $exe -Parent
            $ComfyUiPath = $parent
        }
    }
}

if (-not $ComfyUiPath -and $Interactive) {
    $defaultDir = Join-Path $env:USERPROFILE "AI\ComfyUI"
    $inputDir = Read-Host "Enter ComfyUI directory [Default: $defaultDir]"
    if ($inputDir) {
        $ComfyUiPath = $inputDir
    } else {
        $ComfyUiPath = $defaultDir
    }
}

if (-not $ComfyUiPath) {
    $candidates = @(
        (Join-Path $env:USERPROFILE "AI\ComfyUI"),
        (Join-Path $env:USERPROFILE "ComfyUI"),
        (Join-Path $env:SystemDrive "AI\ComfyUI"),
        (Join-Path $env:SystemDrive "ComfyUI")
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $ComfyUiPath = $cand
            break
        }
    }
    if (-not $ComfyUiPath) {
        $ComfyUiPath = Join-Path $env:USERPROFILE "AI\ComfyUI"
    }
}

$customNodesDir = Find-CustomNodesDir -BasePath $ComfyUiPath

if (-not (Test-Path $customNodesDir)) {
    try {
        New-Item -ItemType Directory -Path $customNodesDir -Force | Out-Null
    } catch {
        Write-Host "Error: Could not find or create custom nodes directory at $customNodesDir. Ensure ComfyUI is installed or specify -ComfyUiPath." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Installing ComfyUI Custom Nodes to $customNodesDir..." -ForegroundColor Cyan

$repos = @(
    # 3D Generation Nodes
    "https://github.com/PozzettiAndrea/ComfyUI-TRELLIS2.git",
    "https://github.com/StartHua/ComfyUI-Hunyuan3DWrapper.git",
    "https://github.com/MrForExample/ComfyUI-3D-Pack.git",
    
    # Video Generation Nodes
    "https://github.com/Kosinkadink/ComfyUI-AnimateDiff-Evolved.git",
    "https://github.com/Kosinkadink/ComfyUI-VideoHelperSuite.git",
    "https://github.com/Kijai/ComfyUI-KJNodes.git" # Often useful for mask/video processing
)

Push-Location $customNodesDir
try {
    foreach ($repo in $repos) {
        $folderName = ($repo -split '/')[-1] -replace '\.git$', ''
        $targetFolder = Join-Path $customNodesDir $folderName
        
        if (Test-Path $targetFolder) {
            Write-Host "Updating $folderName..."
            Push-Location $targetFolder
            try {
                git pull
            } finally {
                Pop-Location
            }
        } else {
            Write-Host "Cloning $folderName..."
            git clone $repo $folderName
        }
    }
} finally {
    Pop-Location
}

Write-Host "All custom nodes cloned/updated successfully!" -ForegroundColor Green
Write-Host "Note: Some nodes (like ComfyUI-3D-Pack) may require python packages to be installed. You can do this by running their install.py or through ComfyUI-Manager." -ForegroundColor Yellow
