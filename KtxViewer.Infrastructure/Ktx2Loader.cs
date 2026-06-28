using KtxViewer.Core;
using KtxViewer.Core.Models;
using System.Buffers.Binary;
using System.Text;

namespace KtxViewer.Infrastructure;

public sealed class Ktx2Loader : IKtxLoader
{
    private static ReadOnlySpan<byte> Ktx2Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<KtxImage> LoadAsync(string filePath, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await LoadAsync(stream, cancellationToken, progress);
    }

    public async Task<KtxImage> LoadAsync(Stream stream, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        var header = new byte[80];
        var bytesRead = await stream.ReadAsync(header, cancellationToken);

        if (bytesRead < 80)
        {
            throw new InvalidDataException($"File too small. Expected at least 80 bytes, got {bytesRead}");
        }

        if (!header.AsSpan(0, 12).SequenceEqual(Ktx2Identifier))
        {
            throw new InvalidDataException("Invalid KTX2 file identifier");
        }

        var metadata = await ParseMetadataAsync(stream, header, cancellationToken);

        var vkFormat = metadata.VkFormat;
        var width = metadata.PixelWidth;
        var height = metadata.PixelHeight;
        var levelCount = metadata.LevelCount;
        var layerCount = metadata.LayerCount;

        if (levelCount == 0) levelCount = 1;
        if (layerCount == 0) layerCount = 1;

        var format = MapVulkanFormat(vkFormat);

        if (vkFormat == 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
            progress?.Report(5.0);

            var ktx2File = await stream.ReadAllWithProgressAsync(stream.Length, cancellationToken, progress);
            progress?.Report(80.0);

            if (ktx2File.Length < stream.Length)
            {
                throw new InvalidDataException($"Cannot read complete KTX2 file. Expected {stream.Length} bytes, got {ktx2File.Length}");
            }

            try
            {
                using var transcoder = new LibKtxTranscoder();
                var transcodedData = transcoder.TryTranscode(ktx2File, width, height);
                progress?.Report(100.0);

                if (transcodedData != null)
                {
                    return new KtxImage
                    {
                        Width = (int)width,
                        Height = (int)height,
                        Format = TextureFormat.RGBA8,
                        PixelData = transcodedData,
                        MipLevels = (int)levelCount,
                        LayerCount = (int)layerCount,
                        Metadata = metadata
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
                LayerCount = (int)layerCount,
                Metadata = metadata
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
        await stream.ReadExactlyWithProgressAsync(imageData, stream.Length, cancellationToken, progress);
        progress?.Report(100.0);

        var pixelData = ConvertToRgba8(imageData, (int)width, (int)height, format);

        return new KtxImage
        {
            Width = (int)width,
            Height = (int)height,
            Format = format,
            PixelData = pixelData,
            MipLevels = (int)levelCount,
            LayerCount = (int)layerCount,
            Metadata = metadata
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

    private async Task<KtxMetadata> ParseMetadataAsync(Stream stream, byte[] header, CancellationToken cancellationToken)
    {
        var reader = new ByteStreamReader(header);

        const uint IdentifierLengthBytes = 12;
        reader.Skip(IdentifierLengthBytes);

        var vkFormat = reader.ReadUInt32();
        var typeSize = reader.ReadUInt32();
        var width = reader.ReadUInt32();
        var height = reader.ReadUInt32();
        var depth = reader.ReadUInt32();
        var layerCount = reader.ReadUInt32();
        var faceCount = reader.ReadUInt32();
        var levelCount = reader.ReadUInt32();
        var supercompressionScheme = reader.ReadUInt32();

        var dfdByteOffset = reader.ReadUInt32();
        var dfdByteLength = reader.ReadUInt32();
        var kvdByteOffset = reader.ReadUInt32();
        var kvdByteLength = reader.ReadUInt32();

        var metadata = new KtxMetadata
        {
            VkFormat = vkFormat,
            TypeSize = typeSize,
            PixelWidth = width,
            PixelHeight = height,
            PixelDepth = depth,
            LayerCount = layerCount,
            FaceCount = faceCount,
            LevelCount = levelCount,
            SupercompressionScheme = supercompressionScheme
        };

        // Parse Key/Value Data
        if (kvdByteLength > 0)
        {
            stream.Seek(kvdByteOffset, SeekOrigin.Begin);
            var kvdBuffer = new byte[kvdByteLength];
            await stream.ReadExactlyAsync(kvdBuffer, cancellationToken);
            ParseKeyValueData(metadata, kvdBuffer);
        }

        // Parse DFD
        if (dfdByteLength > 0)
        {
            stream.Seek(dfdByteOffset, SeekOrigin.Begin);
            var dfdBuffer = new byte[dfdByteLength];
            await stream.ReadExactlyAsync(dfdBuffer, cancellationToken);
            ParseDfd(metadata, dfdBuffer);
        }

        return metadata;
    }

    private void ParseKeyValueData(KtxMetadata metadata, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            if (offset + 4 > buffer.Length) break;
            var len = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset));
            offset += 4;

            if (offset + len > buffer.Length) break;

            var kvSpan = buffer.AsSpan(offset, (int)len);
            int nullIndex = -1;
            for (int i = 0; i < kvSpan.Length; i++)
            {
                if (kvSpan[i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }

            if (nullIndex > 0)
            {
                var key = Encoding.UTF8.GetString(kvSpan.Slice(0, nullIndex));
                var valueSpan = kvSpan.Slice(nullIndex + 1);
                if (valueSpan.Length > 0 && valueSpan[valueSpan.Length - 1] == 0)
                {
                    valueSpan = valueSpan.Slice(0, valueSpan.Length - 1);
                }

                string value;
                try
                {
                    bool isBinary = false;
                    for(int i=0; i<valueSpan.Length; i++)
                    {
                        byte b = valueSpan[i];
                        if (b < 32 && b != 9 && b != 10 && b != 13) { isBinary = true; break; }
                    }

                    if (isBinary)
                    {
                        value = BitConverter.ToString(valueSpan.ToArray()).Replace("-", " ");
                    }
                    else
                    {
                        value = Encoding.UTF8.GetString(valueSpan);
                    }
                }
                catch
                {
                    value = BitConverter.ToString(valueSpan.ToArray()).Replace("-", " ");
                }

                metadata.KeyValuePairs[key] = value;
            }

            offset += (int)len;

            // Padding
            int padding = 3 - ((int)len + 3) % 4;
            offset += padding;
        }
    }

    private void ParseDfd(KtxMetadata metadata, byte[] buffer)
    {
        if (buffer.Length < 16) return;

        var colorInfo = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12));
        var colorModel = (byte)(colorInfo & 0xFF);
        var colorPrimaries = (byte)((colorInfo >> 8) & 0xFF);
        var transferFunction = (byte)((colorInfo >> 16) & 0xFF);

        metadata.ColorModel = GetColorModelName(colorModel);
        metadata.ColorPrimaries = GetColorPrimariesName(colorPrimaries);
        metadata.TransferFunction = GetTransferFunctionName(transferFunction);
    }

    private string GetColorModelName(byte model)
    {
        return model switch
        {
            0 => "Unspecified",
            1 => "RGBSDA",
            2 => "YUVSDA",
            3 => "YIQ",
            4 => "Lab",
            128 => "BC1A",
            129 => "BC2",
            130 => "BC3",
            131 => "BC4",
            132 => "BC5",
            133 => "BC6H",
            134 => "BC7",
            163 => "ETC1S",
            166 => "UASTC",
            _ => $"Unknown ({model})"
        };
    }

    private string GetColorPrimariesName(byte primaries)
    {
        return primaries switch
        {
            0 => "Unspecified",
            1 => "BT.709 (sRGB)",
            2 => "BT.601 EBU",
            3 => "BT.601 SMPTE",
            4 => "BT.2020",
            5 => "CIEXYZ",
            6 => "ACES",
            7 => "ACEScc",
            8 => "NTSC 1953",
            9 => "PAL 525",
            10 => "Display P3",
            11 => "Adobe RGB",
            _ => $"Unknown ({primaries})"
        };
    }

    private string GetTransferFunctionName(byte tf)
    {
        return tf switch
        {
            0 => "Unspecified",
            1 => "Linear",
            2 => "sRGB",
            3 => "ITU",
            4 => "NTSC",
            5 => "PAL",
            6 => "BT.709",
            7 => "BT.2020",
            8 => "ADM",
            9 => "HLG",
            10 => "PQ (Perceptual Quantizer)",
            _ => $"Unknown ({tf})"
        };
    }
}
