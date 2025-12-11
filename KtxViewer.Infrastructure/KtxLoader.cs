using KtxViewer.Core;
using System.Buffers.Binary;
using System.Text;

namespace KtxViewer.Infrastructure;

public sealed class KtxLoader : IKtxLoader
{
    private static ReadOnlySpan<byte> KtxIdentifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<KtxImage> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        var header = new byte[80];
        var bytesRead = await stream.ReadAsync(header, cancellationToken);

        if (bytesRead < 80)
        {
            throw new InvalidDataException($"File too small. Expected at least 80 bytes, got {bytesRead}");
        }

        if (!header.AsSpan(0, 12).SequenceEqual(KtxIdentifier))
        {
            throw new InvalidDataException("Invalid KTX2 file identifier");
        }

        var vkFormat = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
        var typeSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16));
        var width = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20));
        var height = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24));
        var depth = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28));
        var levelCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(32));
        var faceCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(36));
        var layerCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40));

        if (levelCount == 0) levelCount = 1;
        if (layerCount == 0) layerCount = 1;

        var format = MapVulkanFormat(vkFormat);

        if (vkFormat == 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var ktx2File = new byte[stream.Length];
            bytesRead = await stream.ReadAsync(ktx2File, cancellationToken);

            if (bytesRead < ktx2File.Length)
            {
                throw new InvalidDataException($"Cannot read complete KTX2 file. Expected {ktx2File.Length} bytes, got {bytesRead}");
            }

            try
            {
                using var transcoder = new LibKtxTranscoder();
                var transcodedData = transcoder.TryTranscode(ktx2File, width, height);

                if (transcodedData != null)
                {
                    return new KtxImage
                    {
                        Width = (int)width,
                        Height = (int)height,
                        Format = TextureFormat.RGBA8,
                        PixelData = transcodedData,
                        MipLevels = (int)levelCount,
                        LayerCount = (int)layerCount
                    };
                }
            }
            catch
            {
            }

            return new KtxImage
            {
                Width = (int)width,
                Height = (int)height,
                Format = TextureFormat.ETC1S,
                PixelData = CreateInfoPlaceholder((int)width, (int)height, "BasisU ETC1S Compressed").ToArray(),
                MipLevels = (int)levelCount,
                LayerCount = (int)layerCount
            };
        }

        var levelIndexOffset = 80uL;

        stream.Seek((long)levelIndexOffset, SeekOrigin.Begin);

        var levelIndex = new byte[24 * levelCount];
        bytesRead = await stream.ReadAsync(levelIndex, cancellationToken);

        if (bytesRead < 24)
        {
            throw new InvalidDataException($"Cannot read level index. Expected at least 24 bytes, got {bytesRead}");
        }

        var imageByteOffset = BinaryPrimitives.ReadUInt64LittleEndian(levelIndex.AsSpan(0));
        var imageByteLength = BinaryPrimitives.ReadUInt64LittleEndian(levelIndex.AsSpan(8));
        var imageUncompressedLength = BinaryPrimitives.ReadUInt64LittleEndian(levelIndex.AsSpan(16));

        if (imageByteOffset == 0 || imageByteLength == 0)
        {
            throw new InvalidDataException("Invalid image data offset/length in level index");
        }

        var fileLength = stream.Length;
        if (imageByteOffset + imageByteLength > (ulong)fileLength)
        {
            throw new InvalidDataException($"Image data extends beyond file boundaries. File size: {fileLength}, required: {imageByteOffset + imageByteLength}");
        }

        stream.Seek((long)imageByteOffset, SeekOrigin.Begin);
        var imageData = new byte[imageByteLength];
        bytesRead = await stream.ReadAsync(imageData, cancellationToken);

        if (bytesRead < (int)imageByteLength)
        {
            throw new InvalidDataException($"Cannot read image data. Expected {imageByteLength} bytes, got {bytesRead}");
        }

        var pixelData = ConvertToRgba8(imageData, (int)width, (int)height, format);

        return new KtxImage
        {
            Width = (int)width,
            Height = (int)height,
            Format = format,
            PixelData = pixelData,
            MipLevels = (int)levelCount,
            LayerCount = (int)layerCount
        };
    }

    private static TextureFormat MapVulkanFormat(uint vkFormat)
    {
        return vkFormat switch
        {
            0 => TextureFormat.ETC1S,
            37 => TextureFormat.RGBA8,
            29 => TextureFormat.RGB8,
            131 => TextureFormat.BC1,
            135 => TextureFormat.BC3,
            145 => TextureFormat.BC7,
            _ => TextureFormat.Unknown
        };
    }

    private static ReadOnlyMemory<byte> ConvertToRgba8(byte[] data, int width, int height, TextureFormat format)
    {
        if (format == TextureFormat.RGBA8)
        {
            return data;
        }

        if (format == TextureFormat.RGB8)
        {
            var rgba = new byte[width * height * 4];
            for (int i = 0, j = 0; i < data.Length; i += 3, j += 4)
            {
                rgba[j] = data[i];
                rgba[j + 1] = data[i + 1];
                rgba[j + 2] = data[i + 2];
                rgba[j + 3] = 255;
            }
            return rgba;
        }

        var placeholder = new byte[width * height * 4];
        var color = format switch
        {
            TextureFormat.BC1 => (r: 255, g: 150, b: 100),
            TextureFormat.BC3 => (r: 150, g: 255, b: 100),
            TextureFormat.BC7 => (r: 255, g: 100, b: 255),
            TextureFormat.ETC1S => (r: 100, g: 150, b: 255),
            _ => (r: 128, g: 128, b: 128)
        };

        for (int i = 0; i < placeholder.Length; i += 4)
        {
            placeholder[i] = (byte)color.r;
            placeholder[i + 1] = (byte)color.g;
            placeholder[i + 2] = (byte)color.b;
            placeholder[i + 3] = 255;
        }
        return placeholder;
    }

    private static ReadOnlyMemory<byte> CreateInfoPlaceholder(int width, int height, string message)
    {
        var rgba = new byte[width * height * 4];

        const int stripeHeight = 40;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;

                var stripeIndex = (x + y) / stripeHeight;
                bool isDark = stripeIndex % 2 == 0;

                if (isDark)
                {
                    rgba[index] = 30;
                    rgba[index + 1] = 60;
                    rgba[index + 2] = 120;
                }
                else
                {
                    rgba[index] = 100;
                    rgba[index + 1] = 150;
                    rgba[index + 2] = 220;
                }

                rgba[index + 3] = 255;
            }
        }

        return rgba;
    }
}
