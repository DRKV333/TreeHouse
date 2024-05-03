using System.Text;

namespace TreeHouse.PacketParser.Support;

internal static class Intrinsics
{
    public static bool ReadBool(this SpanReader reader) => reader.ReadByte() != 0;

    public static void WriteBool(this SpanWriter writer, bool b) => writer.WriteByte(b ? (byte)1 : (byte)0);

    public static string ReadCString(this SpanReader reader)
    {
        int length = reader.ReadInt16LE();
        return reader.ReadASCIIFixed(length);
    }

    public static void WriteCString(this SpanWriter writer, string str)
    {
        SpanWriter.Buffer16 lengthBuffer = writer.WriteBuffer16();
        int length = writer.WriteASCII(str);
        lengthBuffer.WriteInt16LE((short)length);
    }

    public static int EstimateCString(string str) => sizeof(short) + Encoding.ASCII.GetMaxByteCount(str.Length);

    public static string ReadWString(this SpanReader reader)
    {
        int lenght = reader.ReadInt16LE() * 2;
        return reader.ReadUTF16LEFixed(lenght);
    }

    public static void WriteWString(this SpanWriter writer, string str)
    {
        SpanWriter.Buffer16 lengthBuffer = writer.WriteBuffer16();
        int lenght = writer.WriteUTF16LE(str);
        lengthBuffer.WriteInt16LE((short)(lenght / 2));
    }

    public static int EstimateWString(string str) => sizeof(short) + Encoding.Unicode.GetMaxByteCount(str.Length);
}
