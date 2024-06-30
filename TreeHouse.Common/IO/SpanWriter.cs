using System;
using System.Buffers.Binary;
using System.Text;

namespace TreeHouse.Common.IO;

public ref struct SpanWriter
{
    public readonly ref struct Buffer16
    {
        public Span<byte> Buffer { get; }
        public Buffer16(SpanWriter writer) => Buffer = writer.BufferNext(2);

        public void WriteHalfLE(Half value) => BinaryPrimitives.WriteHalfLittleEndian(Buffer, value);
        public void WriteInt16LE(Int16 value) => BinaryPrimitives.WriteInt16LittleEndian(Buffer, value);
        public void WriteUInt16LE(UInt16 value) => BinaryPrimitives.WriteUInt16LittleEndian(Buffer, value);
        public void WriteHalfBE(Half value) => BinaryPrimitives.WriteHalfBigEndian(Buffer, value);
        public void WriteInt16BE(Int16 value) => BinaryPrimitives.WriteInt16BigEndian(Buffer, value);
        public void WriteUInt16BE(UInt16 value) => BinaryPrimitives.WriteUInt16BigEndian(Buffer, value);
    }

    public readonly ref struct Buffer32
    {
        public Span<byte> Buffer { get; }
        public Buffer32(SpanWriter writer) => Buffer = writer.BufferNext(4);

        public void WriteSingleLE(Single value) => BinaryPrimitives.WriteSingleLittleEndian(Buffer, value);
        public void WriteInt32LE(Int32 value) => BinaryPrimitives.WriteInt32LittleEndian(Buffer, value);
        public void WriteUInt32LE(UInt32 value) => BinaryPrimitives.WriteUInt32LittleEndian(Buffer, value);
        public void WriteSingleBE(Single value) => BinaryPrimitives.WriteSingleBigEndian(Buffer, value);
        public void WriteInt32BE(Int32 value) => BinaryPrimitives.WriteInt32BigEndian(Buffer, value);
        public void WriteUInt32BE(UInt32 value) => BinaryPrimitives.WriteUInt32BigEndian(Buffer, value);
    }
    
    public readonly ref struct Buffer64
    {
        public Span<byte> Buffer { get; }
        public Buffer64(SpanWriter writer) => Buffer = writer.BufferNext(8);

        public void WriteDoubleLE(Double value) => BinaryPrimitives.WriteDoubleLittleEndian(Buffer, value);
        public void WriteInt64LE(Int64 value) => BinaryPrimitives.WriteInt64LittleEndian(Buffer, value);
        public void WriteUInt64LE(UInt64 value) => BinaryPrimitives.WriteUInt64LittleEndian(Buffer, value);
        public void WriteDoubleBE(Double value) => BinaryPrimitives.WriteDoubleBigEndian(Buffer, value);
        public void WriteInt64BE(Int64 value) => BinaryPrimitives.WriteInt64BigEndian(Buffer, value);
        public void WriteUInt64BE(UInt64 value) => BinaryPrimitives.WriteUInt64BigEndian(Buffer, value);
    }

    public Span<byte> Buffer { get; }
    public int Position { get; set; } = 0;

    public SpanWriter(Span<byte> buffer)
    {
        Buffer = buffer;
    }

    private Span<byte> BufferNext(int size)
    {
        Span<byte> sliced = Buffer.Slice(Position, size);
        Position += size;
        return sliced;
    }

    public void WriteZeroes(int times) => BufferNext(times).Clear();

    public void WriteByte(byte b) => BufferNext(1)[0] = b;

    public void WriteByte(byte b, int times) => BufferNext(times).Fill(b);

    public void WriteSByte(sbyte b) => WriteByte((byte)b);

    public void WriteSByte(sbyte b, int times) => WriteByte((byte)b, times);

    private int WriteString(Encoding encoding, string str)
    {
        int size = encoding.GetBytes(str, Buffer[Position..]);
        Position += size;
        return size;
    }

    public Buffer16 WriteBuffer16() => new Buffer16(this);
    public Buffer32 WriteBuffer32() => new Buffer32(this);
    public Buffer64 WriteBuffer64() => new Buffer64(this);

    public void Write(ReadOnlySpan<byte> bytes) => bytes.CopyTo(BufferNext(bytes.Length));

    public void WriteHalfLE(Half value) => WriteBuffer16().WriteHalfLE(value);
    public void WriteSingleLE(Single value) => WriteBuffer32().WriteSingleLE(value);
    public void WriteDoubleLE(Double value) => WriteBuffer64().WriteDoubleLE(value);

    public void WriteInt16LE(Int16 value) => WriteBuffer16().WriteInt16LE(value);
    public void WriteInt32LE(Int32 value) => WriteBuffer32().WriteInt32LE(value);
    public void WriteInt64LE(Int64 value) => WriteBuffer64().WriteInt64LE(value);

    public void WriteUInt16LE(UInt16 value) => WriteBuffer16().WriteUInt16LE(value);
    public void WriteUInt32LE(UInt32 value) => WriteBuffer32().WriteUInt32LE(value);
    public void WriteUInt64LE(UInt64 value) => WriteBuffer64().WriteUInt64LE(value);

    public void WriteHalfBE(Half value) => WriteBuffer16().WriteHalfBE(value);
    public void WriteSingleBE(Single value) => WriteBuffer32().WriteSingleBE(value);
    public void WriteDoubleBE(Double value) => WriteBuffer64().WriteDoubleBE(value);

    public void WriteInt16BE(Int16 value) => WriteBuffer16().WriteInt16BE(value);
    public void WriteInt32BE(Int32 value) => WriteBuffer32().WriteInt32BE(value);
    public void WriteInt64BE(Int64 value) => WriteBuffer64().WriteInt64BE(value);

    public void WriteUInt16BE(UInt16 value) => WriteBuffer16().WriteUInt16BE(value);
    public void WriteUInt32BE(UInt32 value) => WriteBuffer32().WriteUInt32BE(value);
    public void WriteUInt64BE(UInt64 value) => WriteBuffer64().WriteUInt64BE(value);

    public int WriteASCII(string str) => WriteString(Encoding.ASCII, str);
    public int WriteUTF16LE(string str) => WriteString(Encoding.Unicode, str);
    public int WriteUTF16BE(string str) => WriteString(Encoding.BigEndianUnicode, str);

    public void WriteGuidLE(Guid guid) => guid.TryWriteBytes(BufferNext(16), bigEndian: false, out int _);
    public void WriteGuidBE(Guid guid) => guid.TryWriteBytes(BufferNext(16), bigEndian: true, out int _);
}
