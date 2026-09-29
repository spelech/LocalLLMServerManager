using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// High-performance contour, transparency, and die-cut sticker image processing engine.
/// Provides circular Euclidean dilation, background auto-cutout, and PNG codec utilities.
/// </summary>
public static class StickerContourProcessor
{
    private static readonly uint[] CrcTable = InitializeCrcTable();

    /// <summary>
    /// Applies a die-cut contour border around foreground elements in a 32-bit RGBA pixel buffer.
    /// Preserves original foreground pixel colors and alpha, expanding the outer silhouette
    /// with an opaque pure white border (255, 255, 255, 255).
    /// </summary>
    /// <param name="rgbaPixels">Raw RGBA pixel buffer (length = width * height * 4).</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="borderWidthPx">Radius/thickness of white die-cut border in pixels.</param>
    /// <param name="autoCutout">If true, removes uniform or near-white background starting from borders.</param>
    /// <returns>Modified RGBA pixel buffer.</returns>
    public static byte[] ApplyDieCutBorder(byte[] rgbaPixels, int width, int height, int borderWidthPx, bool autoCutout = true)
    {
        if (rgbaPixels == null || rgbaPixels.Length == 0 || width <= 0 || height <= 0 || rgbaPixels.Length < width * height * 4)
        {
            return Array.Empty<byte>();
        }

        int totalPixels = width * height;
        byte[] output = new byte[totalPixels * 4];

        // 1. Identify background pixels via flood fill if autoCutout is enabled
        bool[] isBackground = new bool[totalPixels];
        if (autoCutout)
        {
            var queue = new Queue<int>();

            bool IsCandidate(int idx)
            {
                int pi = idx * 4;
                byte r = rgbaPixels[pi];
                byte g = rgbaPixels[pi + 1];
                byte b = rgbaPixels[pi + 2];
                byte a = rgbaPixels[pi + 3];

                // Transparent or near-white (diffusion background)
                return a <= 32 || (r >= 235 && g >= 235 && b >= 235);
            }

            void TryEnqueue(int x, int y)
            {
                int idx = y * width + x;
                if (!isBackground[idx] && IsCandidate(idx))
                {
                    isBackground[idx] = true;
                    queue.Enqueue(idx);
                }
            }

            // Seed with all image borders
            for (int x = 0; x < width; x++)
            {
                TryEnqueue(x, 0);
                TryEnqueue(x, height - 1);
            }
            for (int y = 0; y < height; y++)
            {
                TryEnqueue(0, y);
                TryEnqueue(width - 1, y);
            }

            while (queue.Count > 0)
            {
                int curr = queue.Dequeue();
                int cx = curr % width;
                int cy = curr / width;

                if (cx > 0) TryEnqueue(cx - 1, cy);
                if (cx < width - 1) TryEnqueue(cx + 1, cy);
                if (cy > 0) TryEnqueue(cx, cy - 1);
                if (cy < height - 1) TryEnqueue(cx, cy + 1);
            }
        }

        bool IsForeground(int idx)
        {
            if (autoCutout && isBackground[idx]) return false;
            return rgbaPixels[idx * 4 + 3] > 32;
        }

        // 2. If no border dilation requested, just apply foreground preservation and cutout
        if (borderWidthPx <= 0)
        {
            for (int idx = 0; idx < totalPixels; idx++)
            {
                int pi = idx * 4;
                if (IsForeground(idx))
                {
                    output[pi + 0] = rgbaPixels[pi + 0];
                    output[pi + 1] = rgbaPixels[pi + 1];
                    output[pi + 2] = rgbaPixels[pi + 2];
                    output[pi + 3] = rgbaPixels[pi + 3];
                }
                else if (!autoCutout)
                {
                    output[pi + 0] = rgbaPixels[pi + 0];
                    output[pi + 1] = rgbaPixels[pi + 1];
                    output[pi + 2] = rgbaPixels[pi + 2];
                    output[pi + 3] = rgbaPixels[pi + 3];
                }
            }
            return output;
        }

        // 3. Circular Euclidean dilation: dx^2 + dy^2 <= R^2
        int r2 = borderWidthPx * borderWidthPx;
        var circleOffsets = new List<(int dx, int dy)>();
        for (int dy = -borderWidthPx; dy <= borderWidthPx; dy++)
        {
            int dy2 = dy * dy;
            for (int dx = -borderWidthPx; dx <= borderWidthPx; dx++)
            {
                if (dx * dx + dy2 <= r2)
                {
                    circleOffsets.Add((dx, dy));
                }
            }
        }

        bool[] isBorder = new bool[totalPixels];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (!IsForeground(idx)) continue;

                // Optimization: Only foreground boundary pixels radiate dilation
                bool isEdge = (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                              || !IsForeground(idx - 1)
                              || !IsForeground(idx + 1)
                              || !IsForeground(idx - width)
                              || !IsForeground(idx + width);

                if (!isEdge) continue;

                foreach (var (dx, dy) in circleOffsets)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        isBorder[ny * width + nx] = true;
                    }
                }
            }
        }

        // 4. Compose final die-cut output: foreground layered over white border
        for (int idx = 0; idx < totalPixels; idx++)
        {
            int pi = idx * 4;
            if (IsForeground(idx))
            {
                output[pi + 0] = rgbaPixels[pi + 0];
                output[pi + 1] = rgbaPixels[pi + 1];
                output[pi + 2] = rgbaPixels[pi + 2];
                output[pi + 3] = rgbaPixels[pi + 3];
            }
            else if (isBorder[idx])
            {
                // Crisp die-cut white border
                output[pi + 0] = 255;
                output[pi + 1] = 255;
                output[pi + 2] = 255;
                output[pi + 3] = 255;
            }
            else
            {
                // Fully transparent background
                output[pi + 0] = 0;
                output[pi + 1] = 0;
                output[pi + 2] = 0;
                output[pi + 3] = 0;
            }
        }

        return output;
    }

    /// <summary>
    /// Creates a minimal solid color PNG image of the specified dimensions.
    /// </summary>
    public static byte[] CreateMinimalTestPng(int width, int height, byte r, byte g, byte b, byte a)
    {
        if (width <= 0 || height <= 0) return Array.Empty<byte>();
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }
        return EncodePng(pixels, width, height);
    }

    /// <summary>
    /// Decodes a PNG byte buffer into raw 32-bit RGBA pixels.
    /// </summary>
    public static byte[] DecodePng(byte[] pngBytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (pngBytes == null || pngBytes.Length < 8) return Array.Empty<byte>();

        // Verify PNG signature: 89 50 4E 47 0D 0A 1A 0A
        if (pngBytes[0] != 0x89 || pngBytes[1] != 0x50 || pngBytes[2] != 0x4E || pngBytes[3] != 0x47 ||
            pngBytes[4] != 0x0D || pngBytes[5] != 0x0A || pngBytes[6] != 0x1A || pngBytes[7] != 0x0A)
        {
            return Array.Empty<byte>();
        }

        int offset = 8;
        byte bitDepth = 8;
        byte colorType = 6;
        byte interlace = 0;
        using var idatStream = new MemoryStream();

        while (offset + 8 <= pngBytes.Length)
        {
            int length = ReadBigEndianInt32(pngBytes, offset);
            offset += 4;
            string type = System.Text.Encoding.ASCII.GetString(pngBytes, offset, 4);
            offset += 4;

            if (offset + length > pngBytes.Length) break;

            if (type == "IHDR" && length >= 13)
            {
                width = ReadBigEndianInt32(pngBytes, offset);
                height = ReadBigEndianInt32(pngBytes, offset + 4);
                bitDepth = pngBytes[offset + 8];
                colorType = pngBytes[offset + 9];
                interlace = pngBytes[offset + 12];
            }
            else if (type == "IDAT")
            {
                idatStream.Write(pngBytes, offset, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset += length + 4; // Skip data + 4 bytes CRC
        }

        if (width <= 0 || height <= 0 || bitDepth != 8 || interlace != 0)
        {
            return Array.Empty<byte>();
        }

        byte[] compressedData = idatStream.ToArray();
        if (compressedData.Length == 0) return Array.Empty<byte>();

        byte[] decompressed;
        using (var compressedMs = new MemoryStream(compressedData))
        using (var zlib = new ZLibStream(compressedMs, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
            zlib.CopyTo(outMs);
            decompressed = outMs.ToArray();
        }

        int bytesPerPixel = colorType switch
        {
            6 => 4, // RGBA
            2 => 3, // RGB
            _ => 0
        };

        if (bytesPerPixel == 0) return Array.Empty<byte>();

        int stride = width * bytesPerPixel;
        int expectedLength = height * (1 + stride);
        if (decompressed.Length < expectedLength) return Array.Empty<byte>();

        byte[] reconstructedScanlines = new byte[height * stride];

        for (int y = 0; y < height; y++)
        {
            int srcRowStart = y * (1 + stride);
            byte filterType = decompressed[srcRowStart];
            int dstRowStart = y * stride;
            int prevRowStart = (y - 1) * stride;

            for (int x = 0; x < stride; x++)
            {
                byte raw = decompressed[srcRowStart + 1 + x];
                byte a = (x >= bytesPerPixel) ? reconstructedScanlines[dstRowStart + x - bytesPerPixel] : (byte)0;
                byte b = (y > 0) ? reconstructedScanlines[prevRowStart + x] : (byte)0;
                byte c = (y > 0 && x >= bytesPerPixel) ? reconstructedScanlines[prevRowStart + x - bytesPerPixel] : (byte)0;

                byte recon = filterType switch
                {
                    0 => raw,
                    1 => (byte)(raw + a),
                    2 => (byte)(raw + b),
                    3 => (byte)(raw + ((a + b) / 2)),
                    4 => (byte)(raw + PaethPredictor(a, b, c)),
                    _ => raw
                };

                reconstructedScanlines[dstRowStart + x] = recon;
            }
        }

        // Convert to RGBA
        byte[] rgba = new byte[width * height * 4];
        if (colorType == 6)
        {
            Buffer.BlockCopy(reconstructedScanlines, 0, rgba, 0, rgba.Length);
        }
        else if (colorType == 2)
        {
            for (int i = 0; i < width * height; i++)
            {
                rgba[i * 4 + 0] = reconstructedScanlines[i * 3 + 0];
                rgba[i * 4 + 1] = reconstructedScanlines[i * 3 + 1];
                rgba[i * 4 + 2] = reconstructedScanlines[i * 3 + 2];
                rgba[i * 4 + 3] = 255;
            }
        }

        return rgba;
    }

    /// <summary>
    /// Encodes raw 32-bit RGBA pixels into standard PNG bytes.
    /// </summary>
    public static byte[] EncodePng(byte[] rgbaPixels, int width, int height)
    {
        if (rgbaPixels == null || rgbaPixels.Length == 0 || width <= 0 || height <= 0 || rgbaPixels.Length < width * height * 4)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();

        // 1. Signature
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // 2. IHDR
        byte[] ihdrData = new byte[13];
        WriteBigEndianInt32(ihdrData, 0, width);
        WriteBigEndianInt32(ihdrData, 4, height);
        ihdrData[8] = 8; // 8 bits per channel
        ihdrData[9] = 6; // RGBA color type
        ihdrData[10] = 0; // Deflate compression
        ihdrData[11] = 0; // Filter method 0
        ihdrData[12] = 0; // Non-interlaced
        WriteChunk(ms, "IHDR", ihdrData);

        // 3. IDAT (Scanlines with filter type 0: None)
        int stride = width * 4;
        byte[] rawScanlines = new byte[height * (1 + stride)];
        for (int y = 0; y < height; y++)
        {
            int srcIdx = y * stride;
            int dstIdx = y * (1 + stride);
            rawScanlines[dstIdx] = 0; // Filter 0
            Buffer.BlockCopy(rgbaPixels, srcIdx, rawScanlines, dstIdx + 1, stride);
        }

        byte[] compressedIdat;
        using (var compressedMs = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(rawScanlines, 0, rawScanlines.Length);
            }
            compressedIdat = compressedMs.ToArray();
        }
        WriteChunk(ms, "IDAT", compressedIdat);

        // 4. IEND
        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    /// <summary>
    /// Convenience helper that decodes a PNG, applies the die-cut border with optional auto-cutout,
    /// and returns the encoded result PNG bytes.
    /// </summary>
    public static byte[] ApplyDieCutBorderToPng(byte[] pngBytes, int borderWidthPx, bool autoCutout = true)
    {
        if (pngBytes == null || pngBytes.Length == 0) return Array.Empty<byte>();
        var rgba = DecodePng(pngBytes, out int w, out int h);
        if (rgba.Length == 0 || w <= 0 || h <= 0) return pngBytes;
        var processed = ApplyDieCutBorder(rgba, w, h, borderWidthPx, autoCutout);
        return EncodePng(processed, w, h);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        byte[] lengthBytes = new byte[4];
        WriteBigEndianInt32(lengthBytes, 0, data.Length);

        stream.Write(lengthBytes);
        stream.Write(typeBytes);
        if (data.Length > 0)
        {
            stream.Write(data);
        }

        uint crc = ComputeCrc(typeBytes, data);
        byte[] crcBytes = new byte[4];
        WriteBigEndianUInt32(crcBytes, 0, crc);
        stream.Write(crcBytes);
    }

    private static int ReadBigEndianInt32(byte[] buffer, int offset)
    {
        return (buffer[offset] << 24) |
               (buffer[offset + 1] << 16) |
               (buffer[offset + 2] << 8) |
               buffer[offset + 3];
    }

    private static void WriteBigEndianInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void WriteBigEndianUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static byte PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return (byte)a;
        if (pb <= pc) return (byte)b;
        return (byte)c;
    }

    private static uint[] InitializeCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                if ((c & 1) != 0)
                    c = 0xEDB88320u ^ (c >> 1);
                else
                    c >>= 1;
            }
            table[i] = c;
        }
        return table;
    }

    private static uint ComputeCrc(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < type.Length; i++)
            crc = CrcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
        for (int i = 0; i < data.Length; i++)
            crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
