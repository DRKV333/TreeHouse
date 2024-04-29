using System;

namespace TreeHouse.PacketParser.Support;

internal static class ArrayIntrinsics
{
    public static void ReadArrayStructure<T>(this SpanReader reader, int length, ref T[] array)
        where T : ISpanReadWrite
    {
        array = new T[length];
        for (int i = 0; i < length; i++)
        {
            array[i].Read(reader);
        }
    }

    public static void WriteArrayStructure<T>(this SpanWriter writer, T[] array)
        where T : ISpanReadWrite
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i].Write(writer);
        }
    }

    public static int EstimateArrayStructure<T>(T[] array)
        where T : ISpanReadWrite
    {
        int size = 0;
        for (int i = 0; i < array.Length; i++)
        {
            size += array[i].EstimateSize();
        }
        return size;
    }

    public static int EstimateArrayCString(string[] array)
    {
        int size = 0;
        for (int i = 0; i < array.Length; i++)
        {
            size += Intrinsics.EstimateCString(array[i]);
        }
        return size;
    }
    
    public static int EstimateArrayWString(string[] array)
    {
        int size = 0;
        for (int i = 0; i < array.Length; i++)
        {
            size += Intrinsics.EstimateWString(array[i]);
        }
        return size;
    }

    public static void ReadArrayU8(this SpanReader reader, int length, ref byte[] array)
    {
        array = new byte[length];
        reader.Read(length).CopyTo(array);
    }

    public static void WriteArrayU8(this SpanWriter writer, byte[] array)
    {
        writer.Write(array);
    }

    public static void ReadArrayBool(this SpanReader reader, int length, ref bool[] array)
    {
        array = new bool[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadBool();
        }
    }

    public static void WriteArrayBool(this SpanWriter reader, bool[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteBool(array[i]);
        }
    }

    public static void ReadArrayI8(this SpanReader reader, int length, ref sbyte[] array)
    {
        array = new sbyte[length];
        reader.Read(length).CopyTo((byte[])(Array)array);
    }

    public static void WriteArrayI8(this SpanWriter writer, sbyte[] array)
    {
        writer.Write((byte[])(Array)array);
    }

    public static void ReadArrayU16(this SpanReader reader, int length, ref ushort[] array)
    {
        array = new ushort[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadUInt16LE();
        }
    }

    public static void WriteArrayU16(this SpanWriter reader, ushort[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteUInt16LE(array[i]);
        }
    }
    
    public static void ReadArrayI16(this SpanReader reader, int length, ref short[] array)
    {
        array = new short[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadInt16LE();
        }
    }

    public static void WriteArrayI16(this SpanWriter reader, short[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteInt16LE(array[i]);
        }
    }

    public static void ReadArrayU32(this SpanReader reader, int length, ref uint[] array)
    {
        array = new uint[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadUInt32LE();
        }
    }

    public static void WriteArrayU32(this SpanWriter reader, uint[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteUInt32LE(array[i]);
        }
    }
    
    public static void ReadArrayI32(this SpanReader reader, int length, ref int[] array)
    {
        array = new int[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadInt32LE();
        }
    }

    public static void WriteArrayI32(this SpanWriter reader, int[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteInt32LE(array[i]);
        }
    }

    public static void ReadArrayU64(this SpanReader reader, int length, ref ulong[] array)
    {
        array = new ulong[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadUInt64LE();
        }
    }

    public static void WriteArrayU64(this SpanWriter reader, ulong[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteUInt64LE(array[i]);
        }
    }

    public static void ReadArrayI64(this SpanReader reader, int length, ref long[] array)
    {
        array = new long[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadInt64LE();
        }
    }

    public static void WriteArrayI64(this SpanWriter reader, long[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteInt64LE(array[i]);
        }
    }

    public static void ReadArrayF32(this SpanReader reader, int length, ref float[] array)
    {
        array = new float[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadSingleLE();
        }
    }

    public static void WriteArrayF32(this SpanWriter reader, float[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteSingleLE(array[i]);
        }
    }

    public static void ReadArrayF64(this SpanReader reader, int length, ref double[] array)
    {
        array = new double[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadDoubleLE();
        }
    }

    public static void WriteArrayF64(this SpanWriter reader, double[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteDoubleLE(array[i]);
        }
    }

    public static void ReadArrayCString(this SpanReader reader, int length, ref string[] array)
    {
        array = new string[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadCString();
        }
    }

    public static void WriteArrayCString(this SpanWriter reader, string[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteCString(array[i]);
        }
    }
    
    public static void ReadArrayWString(this SpanReader reader, int length, ref string[] array)
    {
        array = new string[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadWString();
        }
    }

    public static void WriteArrayWString(this SpanWriter reader, string[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteWString(array[i]);
        }
    }
    
    public static void ReadArrayUUID(this SpanReader reader, int length, ref Guid[] array)
    {
        array = new Guid[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadGuidLE();
        }
    }

    public static void WriteArrayUUID(this SpanWriter reader, Guid[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            reader.WriteGuidLE(array[i]);
        }
    }
}
