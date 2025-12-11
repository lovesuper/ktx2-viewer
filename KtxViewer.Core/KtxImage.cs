namespace KtxViewer.Core;

public sealed class KtxImage
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required TextureFormat Format { get; init; }
    public required ReadOnlyMemory<byte> PixelData { get; init; }
    public int MipLevels { get; init; } = 1;
    public int LayerCount { get; init; } = 1;
    public Models.KtxMetadata? Metadata { get; init; }
}
