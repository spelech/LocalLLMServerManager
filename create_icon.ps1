$dir = "C:\Users\Alias\repos\LocalLLMServerManager\Assets"
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force
}

$icoPath = Join-Path $dir "app-icon.ico"

# Create valid 32x32 32bpp ICO file
$stream = [System.IO.File]::Create($icoPath)
$writer = [System.IO.BinaryWriter]::new($stream)

# ICONHEADER (6 bytes)
$writer.Write([UInt16]0) # Reserved
$writer.Write([UInt16]1) # Type (1 = ICO)
$writer.Write([UInt16]1) # Count (1 image)

# ICONDIRENTRY (16 bytes)
$writer.Write([byte]32) # Width
$writer.Write([byte]32) # Height
$writer.Write([byte]0)  # Color palette
$writer.Write([byte]0)  # Reserved
$writer.Write([UInt16]1) # Color planes
$writer.Write([UInt16]32) # Bits per pixel
$imgDataSize = 40 + (32 * 32 * 4) + 128 # BITMAPINFOHEADER (40) + Pixels (4096) + AND Mask (128)
$writer.Write([UInt32]$imgDataSize)
$writer.Write([UInt32]22) # Offset to image data

# BITMAPINFOHEADER (40 bytes)
$writer.Write([UInt32]40) # Header size
$writer.Write([Int32]32)  # Width
$writer.Write([Int32]64)  # Height (32 * 2 for icon mask)
$writer.Write([UInt16]1)  # Planes
$writer.Write([UInt16]32) # Bpp
$writer.Write([UInt32]0)  # Compression
$writer.Write([UInt32](32 * 32 * 4)) # Image size
$writer.Write([Int32]0)   # X pixels per meter
$writer.Write([Int32]0)   # Y pixels per meter
$writer.Write([UInt32]0)  # Colors used
$writer.Write([UInt32]0)  # Colors important

# Pixel data: 32x32 RGBA (BGRA format)
for ($i = 0; $i -lt (32 * 32); $i++) {
    $writer.Write([byte]42) # Blue
    $writer.Write([byte]23) # Green
    $writer.Write([byte]15) # Red
    $writer.Write([byte]255) # Alpha
}

# AND mask (128 bytes of zeros)
for ($i = 0; $i -lt 128; $i++) {
    $writer.Write([byte]0)
}

$writer.Close()
$stream.Close()
Write-Host "Created valid app-icon.ico asset!"
