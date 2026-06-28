using System.Buffers.Binary;

namespace KtxViewer.Infrastructure;

public ref struct ByteStreamReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private uint _offset = 0;

    public uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice((int)_offset, 4));
        _offset += 4;
        return value;
    }

    public void Skip(uint byteCount)
    {
        _offset += byteCount;
    }
}
