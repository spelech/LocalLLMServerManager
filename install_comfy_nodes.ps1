# Script to install ComfyUI custom nodes for 3D and Video Generation

$customNodesDir = "D:\AI\ComfyUI\ComfyUI\custom_nodes"

if (-not (Test-Path $customNodesDir)) {
    Write-Host "Error: Custom nodes directory not found at $customNodesDir. Ensure ComfyUI is installed first." -ForegroundColor Red
    exit
}

Write-Host "Installing ComfyUI Custom Nodes to $customNodesDir..." -ForegroundColor Cyan

$repos = @(
    # 3D Generation Nodes
    "https://github.com/Vchitect/ComfyUI-Trellis.git",
    "https://github.com/kijai/ComfyUI-Hunyuan3DWrapper.git",
    "https://github.com/MrForExample/ComfyUI-3D-Pack.git",
    
    # Video Generation Nodes
    "https://github.com/Kosinkadink/ComfyUI-AnimateDiff-Evolved.git",
    "https://github.com/Kosinkadink/ComfyUI-VideoHelperSuite.git",
    "https://github.com/Kijai/ComfyUI-KJNodes.git" # Often useful for mask/video processing
)

Set-Location $customNodesDir

foreach ($repo in $repos) {
    $folderName = ($repo -split '/')[-1] -replace '\.git$', ''
    
    if (Test-Path $folderName) {
        Write-Host "Updating $folderName..."
        Set-Location $folderName
        git pull
        Set-Location $customNodesDir
    } else {
        Write-Host "Cloning $folderName..."
        git clone $repo
    }
}

Write-Host "All custom nodes cloned/updated successfully!" -ForegroundColor Green
Write-Host "Note: Some nodes (like ComfyUI-3D-Pack) may require python packages to be installed. You can do this by running their install.py or through ComfyUI-Manager." -ForegroundColor Yellow
