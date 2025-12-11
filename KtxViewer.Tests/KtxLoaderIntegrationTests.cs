using KtxViewer.Infrastructure;
using KtxViewer.Core;

namespace KtxViewer.Tests;

public sealed class KtxLoaderIntegrationTests
{
    [Fact]
    public async Task LoadAsync_WithRealKtx2File_LoadsSuccessfully()
    {
        var loader = new KtxLoader();
        var testFilePath = Path.Combine("..", "..", "..", "..", "test.ktx2");

        if (!File.Exists(testFilePath))
        {
            return;
        }

        var image = await loader.LoadAsync(testFilePath);

        Assert.NotNull(image);
        Assert.True(image.Width > 0, $"Width should be positive, got {image.Width}");
        Assert.True(image.Height > 0, $"Height should be positive, got {image.Height}");
        Assert.NotEqual(TextureFormat.Unknown, image.Format);
        Assert.True(image.PixelData.Length > 0, "PixelData should not be empty");
        Assert.Equal(image.Width * image.Height * 4, image.PixelData.Length);
    }
}
