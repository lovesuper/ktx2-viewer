using KtxViewer.Core;

namespace KtxViewer.Tests;

public sealed class KtxImageTests
{
    [Fact]
    public void KtxImage_WithValidData_CreatesSuccessfully()
    {
        var image = new KtxImage
        {
            Width = 256,
            Height = 256,
            Format = TextureFormat.RGBA8,
            PixelData = new byte[256 * 256 * 4],
            MipLevels = 1,
            LayerCount = 1
        };

        Assert.Equal(256, image.Width);
        Assert.Equal(256, image.Height);
        Assert.Equal(TextureFormat.RGBA8, image.Format);
        Assert.Equal(1, image.MipLevels);
        Assert.Equal(1, image.LayerCount);
    }

    [Fact]
    public void KtxImage_PixelDataLength_MatchesDimensions()
    {
        const int width = 128;
        const int height = 128;
        var pixelData = new byte[width * height * 4];

        var image = new KtxImage
        {
            Width = width,
            Height = height,
            Format = TextureFormat.RGBA8,
            PixelData = pixelData
        };

        Assert.Equal(width * height * 4, image.PixelData.Length);
    }
}
