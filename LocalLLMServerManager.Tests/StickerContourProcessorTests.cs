using System;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StickerContourProcessorTests
{
    [Fact]
    public void ApplyDieCutBorder_HandlesEmptyOrSmallBufferGracefully()
    {
        // Null buffer
        byte[]? nullBuffer = null;
        var resultNull = StickerContourProcessor.ApplyDieCutBorder(nullBuffer!, 10, 10, 2);
        Assert.NotNull(resultNull);
        Assert.Empty(resultNull);

        // Empty buffer
        var resultEmpty = StickerContourProcessor.ApplyDieCutBorder(Array.Empty<byte>(), 10, 10, 2);
        Assert.NotNull(resultEmpty);
        Assert.Empty(resultEmpty);

        // Invalid dimensions
        var dummy = new byte[16];
        var resultZeroWidth = StickerContourProcessor.ApplyDieCutBorder(dummy, 0, 10, 2);
        Assert.Empty(resultZeroWidth);

        var resultZeroHeight = StickerContourProcessor.ApplyDieCutBorder(dummy, 10, 0, 2);
        Assert.Empty(resultZeroHeight);

        // Truncated buffer
        var resultTruncated = StickerContourProcessor.ApplyDieCutBorder(new byte[10], 10, 10, 2);
        Assert.Empty(resultTruncated);
    }

    [Fact]
    public void ApplyDieCutBorder_ZeroBorder_LeavesForegroundIntact()
    {
        int width = 10;
        int height = 10;
        byte[] rgba = new byte[width * height * 4];

        // Place a 2x2 opaque red block at (4,4) to (5,5)
        for (int y = 4; y <= 5; y++)
        {
            for (int x = 4; x <= 5; x++)
            {
                int idx = (y * width + x) * 4;
                rgba[idx + 0] = 255; // R
                rgba[idx + 1] = 0;   // G
                rgba[idx + 2] = 0;   // B
                rgba[idx + 3] = 255; // A
            }
        }

        var processed = StickerContourProcessor.ApplyDieCutBorder(rgba, width, height, borderWidthPx: 0, autoCutout: false);

        Assert.NotNull(processed);
        Assert.Equal(rgba.Length, processed.Length);

        // Check foreground is preserved
        for (int y = 4; y <= 5; y++)
        {
            for (int x = 4; x <= 5; x++)
            {
                int idx = (y * width + x) * 4;
                Assert.Equal(255, processed[idx + 0]);
                Assert.Equal(0, processed[idx + 1]);
                Assert.Equal(0, processed[idx + 2]);
                Assert.Equal(255, processed[idx + 3]);
            }
        }

        // Check background outside remains untouched / transparent
        int bgIdx = (0 * width + 0) * 4;
        Assert.Equal(0, processed[bgIdx + 3]);
    }

    [Fact]
    public void ApplyDieCutBorder_ExpandsContourBySpecifiedRadius()
    {
        int width = 11;
        int height = 11;
        byte[] rgba = new byte[width * height * 4];

        // Put a single blue foreground pixel at center (5, 5)
        int centerIdx = (5 * width + 5) * 4;
        rgba[centerIdx + 0] = 0;   // R
        rgba[centerIdx + 1] = 0;   // G
        rgba[centerIdx + 2] = 255; // B
        rgba[centerIdx + 3] = 255; // A

        // Radius = 2
        var processed = StickerContourProcessor.ApplyDieCutBorder(rgba, width, height, borderWidthPx: 2, autoCutout: false);

        // 1. Center should remain original foreground blue
        Assert.Equal(0, processed[centerIdx + 0]);
        Assert.Equal(0, processed[centerIdx + 1]);
        Assert.Equal(255, processed[centerIdx + 2]);
        Assert.Equal(255, processed[centerIdx + 3]);

        // 2. Neighbor at distance 1 (5, 6) should be white die-cut border
        int d1Idx = (6 * width + 5) * 4;
        Assert.Equal(255, processed[d1Idx + 0]);
        Assert.Equal(255, processed[d1Idx + 1]);
        Assert.Equal(255, processed[d1Idx + 2]);
        Assert.Equal(255, processed[d1Idx + 3]);

        // 3. Neighbor at distance 2 (5, 7) should be white die-cut border
        int d2Idx = (7 * width + 5) * 4;
        Assert.Equal(255, processed[d2Idx + 0]);
        Assert.Equal(255, processed[d2Idx + 1]);
        Assert.Equal(255, processed[d2Idx + 2]);
        Assert.Equal(255, processed[d2Idx + 3]);

        // 4. Diagonal at distance sqrt(1^2 + 1^2) = sqrt(2) <= 2 -> (6, 6) should be white
        int diagIdx = (6 * width + 6) * 4;
        Assert.Equal(255, processed[diagIdx + 0]);
        Assert.Equal(255, processed[diagIdx + 1]);
        Assert.Equal(255, processed[diagIdx + 2]);
        Assert.Equal(255, processed[diagIdx + 3]);

        // 5. Point at dx=1, dy=2 -> 1^2 + 2^2 = 5 > 2^2 -> (6, 7) should NOT be dilated
        int outsideIdx = (7 * width + 6) * 4;
        Assert.Equal(0, processed[outsideIdx + 3]);

        // 6. Far background (0, 0) should remain transparent
        int farIdx = (0 * width + 0) * 4;
        Assert.Equal(0, processed[farIdx + 3]);
    }

    [Fact]
    public void ApplyDieCutBorder_AutoCutout_CutsWhiteBackgroundAndAppliesContour()
    {
        int width = 10;
        int height = 10;
        byte[] rgba = new byte[width * height * 4];

        // Fill background with solid white (255, 255, 255, 255)
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i + 0] = 255;
            rgba[i + 1] = 255;
            rgba[i + 2] = 255;
            rgba[i + 3] = 255;
        }

        // Center 2x2 green foreground at (4,4) to (5,5)
        for (int y = 4; y <= 5; y++)
        {
            for (int x = 4; x <= 5; x++)
            {
                int idx = (y * width + x) * 4;
                rgba[idx + 0] = 0;
                rgba[idx + 1] = 180;
                rgba[idx + 2] = 0;
                rgba[idx + 3] = 255;
            }
        }

        // Apply with autoCutout=true, border=1
        var processed = StickerContourProcessor.ApplyDieCutBorder(rgba, width, height, borderWidthPx: 1, autoCutout: true);

        // Corner (0,0) should have alpha 0 (cutout applied)
        int cornerIdx = (0 * width + 0) * 4;
        Assert.Equal(0, processed[cornerIdx + 3]);

        // Center (4,4) should remain foreground green
        int centerIdx = (4 * width + 4) * 4;
        Assert.Equal(0, processed[centerIdx + 0]);
        Assert.Equal(180, processed[centerIdx + 1]);
        Assert.Equal(0, processed[centerIdx + 2]);
        Assert.Equal(255, processed[centerIdx + 3]);

        // Adjacent border pixel (3,4) at distance 1 should be pure white border (255, 255, 255, 255)
        int borderIdx = (4 * width + 3) * 4;
        Assert.Equal(255, processed[borderIdx + 0]);
        Assert.Equal(255, processed[borderIdx + 1]);
        Assert.Equal(255, processed[borderIdx + 2]);
        Assert.Equal(255, processed[borderIdx + 3]);
    }

    [Fact]
    public void EncodePng_And_DecodePng_RoundTrip()
    {
        int width = 4;
        int height = 4;
        byte[] rgba = new byte[width * height * 4];

        // Fill with pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 4;
                rgba[idx + 0] = (byte)(x * 50);
                rgba[idx + 1] = (byte)(y * 50);
                rgba[idx + 2] = 120;
                rgba[idx + 3] = (byte)(200 + x * 10);
            }
        }

        byte[] png = StickerContourProcessor.EncodePng(rgba, width, height);

        Assert.NotNull(png);
        Assert.True(png.Length > 8);

        // Verify PNG magic bytes
        byte[] expectedHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (int i = 0; i < expectedHeader.Length; i++)
        {
            Assert.Equal(expectedHeader[i], png[i]);
        }

        // Decode back
        var decoded = StickerContourProcessor.DecodePng(png, out int decodedW, out int decodedH);
        Assert.Equal(width, decodedW);
        Assert.Equal(height, decodedH);
        Assert.Equal(rgba.Length, decoded.Length);

        for (int i = 0; i < rgba.Length; i++)
        {
            Assert.Equal(rgba[i], decoded[i]);
        }
    }

    [Fact]
    public void CreateMinimalTestPng_ProducesValidPngWithCorrectDimensions()
    {
        byte[] png = StickerContourProcessor.CreateMinimalTestPng(8, 8, 255, 128, 64, 255);

        Assert.NotNull(png);
        Assert.True(png.Length > 8);

        var decoded = StickerContourProcessor.DecodePng(png, out int w, out int h);
        Assert.Equal(8, w);
        Assert.Equal(8, h);

        int sampleIdx = (4 * 8 + 4) * 4;
        Assert.Equal(255, decoded[sampleIdx + 0]);
        Assert.Equal(128, decoded[sampleIdx + 1]);
        Assert.Equal(64, decoded[sampleIdx + 2]);
        Assert.Equal(255, decoded[sampleIdx + 3]);
    }

    [Fact]
    public void ApplyDieCutBorderToPng_ProcessesValidPngDirectly()
    {
        // 10x10 white background with green center
        byte[] initialPng = StickerContourProcessor.CreateMinimalTestPng(10, 10, 255, 255, 255, 255);
        var decoded = StickerContourProcessor.DecodePng(initialPng, out int w, out int h);

        // Place green dot in center
        int centerIdx = (5 * w + 5) * 4;
        decoded[centerIdx + 0] = 0;
        decoded[centerIdx + 1] = 200;
        decoded[centerIdx + 2] = 0;
        decoded[centerIdx + 3] = 255;

        byte[] inputPng = StickerContourProcessor.EncodePng(decoded, w, h);

        byte[] resultPng = StickerContourProcessor.ApplyDieCutBorderToPng(inputPng, borderWidthPx: 1, autoCutout: true);

        Assert.NotNull(resultPng);
        var finalDecoded = StickerContourProcessor.DecodePng(resultPng, out int fw, out int fh);
        Assert.Equal(10, fw);
        Assert.Equal(10, fh);

        // Corner should be transparent
        Assert.Equal(0, finalDecoded[3]);

        // Center should be green
        Assert.Equal(0, finalDecoded[centerIdx + 0]);
        Assert.Equal(200, finalDecoded[centerIdx + 1]);
        Assert.Equal(0, finalDecoded[centerIdx + 2]);
        Assert.Equal(255, finalDecoded[centerIdx + 3]);

        // Neighbor at (5, 6) should be white border
        int neighborIdx = (6 * w + 5) * 4;
        Assert.Equal(255, finalDecoded[neighborIdx + 0]);
        Assert.Equal(255, finalDecoded[neighborIdx + 1]);
        Assert.Equal(255, finalDecoded[neighborIdx + 2]);
        Assert.Equal(255, finalDecoded[neighborIdx + 3]);
    }
}
