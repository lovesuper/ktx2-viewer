namespace KtxViewer.Core.Models;

public class KtxMetadata
{
    public uint VkFormat { get; set; }
    public uint TypeSize { get; set; }
    public uint PixelWidth { get; set; }
    public uint PixelHeight { get; set; }
    public uint PixelDepth { get; set; }
    public uint LayerCount { get; set; }
    public uint FaceCount { get; set; }
    public uint LevelCount { get; set; }
    public uint SupercompressionScheme { get; set; }
    public Dictionary<string, string> KeyValuePairs { get; set; } = new();
    public string ColorModel { get; set; } = "Unknown";
    public string ColorPrimaries { get; set; } = "Unknown";
    public string TransferFunction { get; set; } = "Unknown";
}
