using System;
using System.Buffers.Binary;
using System.Text;

namespace TreeHouse.Common.IO;

public ref struct SpanReader
{
    public ReadOnlySpan<byte> Buffer { get; }
    public int Position { get; set; } = 0;

    public readonly bool EndOfBuffer => Position >= Buffer.Length;

    public SpanReader(ReadOnlySpan<byte> buffer)
    {
        Buffer = buffer;
    }

    private ReadOnlySpan<byte> BufferNext(int size)
    {
        ReadOnlySpan<byte> sliced = Buffer.Slice(Position, size);
        Position += size;
        return sliced;
    }

    public void Skip(int size) => Position += size;

    public void SkipByte() => Position += 1;

    public void SkipInt16() => Position += sizeof(Int16);
    public void SkipInt32() => Position += sizeof(Int32);
    public void SkipInt64() => Position += sizeof(Int64);

    public ReadOnlySpan<byte> Read(int size) => BufferNext(size);

    public byte ReadByte() => BufferNext(1)[0];
    public sbyte ReadSByte() => (sbyte)BufferNext(1)[0];

    public Half ReadHalfLE() => BinaryPrimitives.ReadHalfLittleEndian(BufferNext(2));
    public Single ReadSingleLE() => BinaryPrimitives.ReadSingleLittleEndian(BufferNext(sizeof(Single)));
    public Double ReadDoubleLE() => BinaryPrimitives.ReadDoubleLittleEndian(BufferNext(sizeof(Double)));

    public Int16 ReadInt16LE() => BinaryPrimitives.ReadInt16LittleEndian(BufferNext(sizeof(Int16)));
    public Int32 ReadInt32LE() => BinaryPrimitives.ReadInt32LittleEndian(BufferNext(sizeof(Int32)));
    public Int64 ReadInt64LE() => BinaryPrimitives.ReadInt64LittleEndian(BufferNext(sizeof(Int64)));

    public UInt16 ReadUInt16LE() => BinaryPrimitives.ReadUInt16LittleEndian(BufferNext(sizeof(UInt16)));
    public UInt32 ReadUInt32LE() => BinaryPrimitives.ReadUInt32LittleEndian(BufferNext(sizeof(UInt32)));
    public UInt64 ReadUInt64LE() => BinaryPrimitives.ReadUInt64LittleEndian(BufferNext(sizeof(UInt64)));
    
    public Half ReadHalfBE() => BinaryPrimitives.ReadHalfBigEndian(BufferNext(2));
    public Single ReadSingleBE() => BinaryPrimitives.ReadSingleBigEndian(BufferNext(sizeof(Single)));
    public Double ReadDoubleBE() => BinaryPrimitives.ReadDoubleBigEndian(BufferNext(sizeof(Double)));

    public Int16 ReadInt16BE() => BinaryPrimitives.ReadInt16BigEndian(BufferNext(sizeof(Int16)));
    public Int32 ReadInt32BE() => BinaryPrimitives.ReadInt32BigEndian(BufferNext(sizeof(Int32)));
    public Int64 ReadInt64BE() => BinaryPrimitives.ReadInt64BigEndian(BufferNext(sizeof(Int64)));

    public UInt16 ReadUInt16BE() => BinaryPrimitives.ReadUInt16BigEndian(BufferNext(sizeof(UInt16)));
    public UInt32 ReadUInt32BE() => BinaryPrimitives.ReadUInt32BigEndian(BufferNext(sizeof(UInt32)));
    public UInt64 ReadUInt64BE() => BinaryPrimitives.ReadUInt64BigEndian(BufferNext(sizeof(UInt64)));

    public string ReadASCIIFixed(int size) => Encoding.ASCII.GetString(BufferNext(size));
    public string ReadUTF16LEFixed(int size) => Encoding.Unicode.GetString(BufferNext(size));
    public string ReadUTF16BEFixed(int size) => Encoding.BigEndianUnicode.GetString(BufferNext(size));

    public Guid ReadGuidLE() => new Guid(BufferNext(16), bigEndian: false);
    public Guid ReadGuidBE() => new Guid(BufferNext(16), bigEndian: true);
}