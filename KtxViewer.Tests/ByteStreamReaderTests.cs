namespace KtxViewer.Infrastructure.Tests;

public sealed class ByteStreamReaderTests
{
    [Fact]
    public void ReadUInt32_ReadsSingleLittleEndianValue()
    {
        var data = new byte[]
        {
            0x78, 0x56, 0x34, 0x12
        };

        var reader = new ByteStreamReader(data);

        var value = reader.ReadUInt32();

        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void ReadUInt32_ReadsMultipleValuesSequentially()
    {
        var data = new byte[]
        {
            // First: 0x01020304
            0x04, 0x03, 0x02, 0x01,
            // Second: 0xAABBCCDD
            0xDD, 0xCC, 0xBB, 0xAA
        };

        var reader = new ByteStreamReader(data);

        var first = reader.ReadUInt32();
        var second = reader.ReadUInt32();

        Assert.Equal(0x01020304u, first);
        Assert.Equal(0xAABBCCDDu, second);
    }

    [Fact]
    public void ReadUInt32_AdvancesOffsetCorrectly()
    {
        var data = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, // 1
            0x02, 0x00, 0x00, 0x00, // 2
            0x03, 0x00, 0x00, 0x00  // 3
        };

        var reader = new ByteStreamReader(data);

        Assert.Equal(1u, reader.ReadUInt32());
        Assert.Equal(2u, reader.ReadUInt32());
        Assert.Equal(3u, reader.ReadUInt32());
    }

    [Fact]
    public void ReadUInt32_ThrowsWhenNotEnoughData()
    {
        var data = new byte[]
        {
            0x01, 0x02, 0x03
        };

        static void Act(ReadOnlySpan<byte> data)
        {
            var reader = new ByteStreamReader(data);
            reader.ReadUInt32();
        }

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Act(data));
    }
}
