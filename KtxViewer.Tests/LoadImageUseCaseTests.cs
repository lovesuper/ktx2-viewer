using KtxViewer.Application;
using KtxViewer.Core;
using Moq;

namespace KtxViewer.Tests;

public sealed class LoadImageUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidFile_ReturnsImage()
    {
        var mockLoader = new Mock<IKtxLoader>();
        var expectedImage = new KtxImage
        {
            Width = 512,
            Height = 512,
            Format = TextureFormat.RGBA8,
            PixelData = new byte[512 * 512 * 4]
        };

        mockLoader
            .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedImage);

        var useCase = new LoadImageUseCase(mockLoader.Object);

        var tempFile = Path.GetTempFileName();
        try
        {
            var result = await useCase.ExecuteAsync(tempFile);

            Assert.NotNull(result);
            Assert.Equal(512, result.Width);
            Assert.Equal(512, result.Height);
            Assert.Equal(TextureFormat.RGBA8, result.Format);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        var mockLoader = new Mock<IKtxLoader>();
        var useCase = new LoadImageUseCase(mockLoader.Object);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => useCase.ExecuteAsync("nonexistent.ktx2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithInvalidPath_ThrowsArgumentException(string? path)
    {
        var mockLoader = new Mock<IKtxLoader>();
        var useCase = new LoadImageUseCase(mockLoader.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(path!));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ThrowsArgumentNullException()
    {
        var mockLoader = new Mock<IKtxLoader>();
        var useCase = new LoadImageUseCase(mockLoader.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));
    }
}
