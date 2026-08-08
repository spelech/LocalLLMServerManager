# Script to download and install ComfyUI and SD Forge to D:\AI

$comfyUrl = "https://github.com/comfyanonymous/ComfyUI/releases/latest/download/ComfyUI_windows_portable_nvidia.7z"
$comfyZip = "D:\AI\ComfyUI_windows_portable.7z"

$forgeUrl = "https://github.com/lllyasviel/stable-diffusion-webui-forge/releases/latest/download/WebUI_Forge_cu121_torch231.7z"
$forgeZip = "D:\AI\WebUI_Forge.7z"

$7zPath = "C:\Program Files\7-Zip\7z.exe"
if (-not (Test-Path $7zPath)) {
    Write-Host "7-Zip not found at $7zPath. Please ensure it is installed." -ForegroundColor Red
    exit
}

Write-Host "Downloading ComfyUI Portable..."
Invoke-WebRequest -Uri $comfyUrl -OutFile $comfyZip

Write-Host "Extracting ComfyUI..."
& $7zPath x $comfyZip -o"D:\AI\" -y
Rename-Item -Path "D:\AI\ComfyUI_windows_portable" -NewName "ComfyUI" -ErrorAction SilentlyContinue

Write-Host "Downloading SD WebUI Forge..."
Invoke-WebRequest -Uri $forgeUrl -OutFile $forgeZip

Write-Host "Extracting SD Forge..."
& $7zPath x $forgeZip -o"D:\AI\" -y
Rename-Item -Path "D:\AI\WebUI" -NewName "SD_Forge" -ErrorAction SilentlyContinue

Write-Host "Cleaning up archives..."
Remove-Item $comfyZip
Remove-Item $forgeZip

Write-Host "Configuring ComfyUI to use shared models..."
$extraModelPaths = @"
comfyui:
    base_path: D:\AI\models
    checkpoints: checkpoints
    loras: loras
    vae: vae
    controlnet: controlnet
"@
$extraModelPaths | Out-File -FilePath "D:\AI\ComfyUI\ComfyUI\extra_model_paths.yaml" -Encoding utf8

Write-Host "Installing ComfyUI Manager (Essential for installing Trellis and other nodes)..."
git clone https://github.com/ltdrdata/ComfyUI-Manager.git "D:\AI\ComfyUI\ComfyUI\custom_nodes\ComfyUI-Manager"

Write-Host "Setup Scripts Completed successfully!"
