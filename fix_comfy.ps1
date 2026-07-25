if (Test-Path "D:\AI\ComfyUI_windows_portable") {
    if (Test-Path "D:\AI\ComfyUI") { Remove-Item "D:\AI\ComfyUI" -Recurse -Force }
    Rename-Item "D:\AI\ComfyUI_windows_portable" -NewName "ComfyUI"
}

if (Test-Path "D:\AI\WebUI") {
    if (Test-Path "D:\AI\SD_Forge") { Remove-Item "D:\AI\SD_Forge" -Recurse -Force }
    Rename-Item "D:\AI\WebUI" -NewName "SD_Forge"
}

$extraModelPaths = @"
comfyui:
    base_path: D:\AI\models
    checkpoints: checkpoints
    loras: loras
    vae: vae
    controlnet: controlnet
"@
$extraModelPaths | Out-File -FilePath "D:\AI\ComfyUI\ComfyUI\extra_model_paths.yaml" -Encoding utf8

if (-not (Test-Path "D:\AI\ComfyUI\ComfyUI\custom_nodes\ComfyUI-Manager")) {
    git clone https://github.com/ltdrdata/ComfyUI-Manager.git "D:\AI\ComfyUI\ComfyUI\custom_nodes\ComfyUI-Manager"
}

.\install_comfy_nodes.ps1
