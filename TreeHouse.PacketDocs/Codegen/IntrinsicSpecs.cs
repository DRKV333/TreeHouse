using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TreeHouse.PacketDocs.Codegen;

internal record class IntrinsicSpec(
    string CsType,
    Func<string, string> Read,
    Func<string, string> Write,
    int Size,
    Func<string, string>? EstimateSize = null
);

internal record class IntrinsicArraySpec(
    string CsType,
    Func<string, string, string> Read,
    Func<string, string> Write,
    int ElementSize,
    Func<string, string>? EstimateSize = null
);

internal static class IntrinsicSpecs
{
    private static readonly Dictionary<string, IntrinsicSpec> specs = new()
    {
        { "bool", new IntrinsicSpec(
            "bool",
            f => $"{f} = reader.ReadBool();",
            f => $"writer.WriteBool({f});",
            sizeof(byte)
        )},
        { "u8", new IntrinsicSpec(
            "byte",
            f => $"{f} = reader.ReadByte();",
            f => $"writer.WriteByte({f});",
            sizeof(byte)
        )},
        { "u16", new IntrinsicSpec(
            "ushort",
            f => $"{f} = reader.ReadUInt16LE();",
            f => $"writer.WriteUInt16LE({f});",
            sizeof(ushort)
        )},
        { "u32", new IntrinsicSpec(
            "uint",
            f => $"{f} = reader.ReadUInt32LE();",
            f => $"writer.WriteUInt32LE({f});",
            sizeof(uint)
        )},
        { "u64", new IntrinsicSpec(
            "ulong",
            f => $"{f} = reader.ReadUInt64LE();",
            f => $"writer.WriteUInt64LE({f});",
            sizeof(ulong)
        )},
        { "i8", new IntrinsicSpec(
            "sbyte",
            f => $"{f} = reader.ReadSByte();",
            f => $"writer.WriteSByte({f});",
            sizeof(sbyte)
        )},
        { "i16", new IntrinsicSpec(
            "short",
            f => $"{f} = reader.ReadInt16LE();",
            f => $"writer.WriteInt16LE({f});",
            sizeof(short)
        )},
        { "i32", new IntrinsicSpec(
            "int",
            f => $"{f} = reader.ReadInt32LE();",
            f => $"writer.WriteInt32LE({f});",
            sizeof(int)
        )},
        { "i64", new IntrinsicSpec(
            "long",
            f => $"{f} = reader.ReadInt64LE();",
            f => $"writer.WriteInt64LE({f});",
            sizeof(long)
        )},
        { "f32", new IntrinsicSpec(
            "float",
            f => $"{f} = reader.ReadSingleLE();",
            f => $"writer.WriteSingleLE({f});",
            sizeof(float)
        )},
        { "f64", new IntrinsicSpec(
            "double",
            f => $"{f} = reader.ReadDoubleLE();",
            f => $"writer.WriteDoubleLE({f});",
            sizeof(double)
        )},
        { "cstring", new IntrinsicSpec(
            "string",
            f => $"{f} = reader.ReadCString();",
            f => $"writer.WriteCString({f});",
            -1,
            f => $"Intrinsics.EstimateCString({f})"
        )},
        { "wstring", new IntrinsicSpec(
            "string",
            f => $"{f} = reader.ReadCString();",
            f => $"writer.WriteCString({f});",
            -1,
            f => $"Intrinsics.EstimateWString({f})"
        )},
        { "uuid", new IntrinsicSpec(
            "global::System.Guid",
            f => $"{f} = reader.ReadGuidLE();",
            f => $"writer.WriteGuidLE({f});",
            16
        )}
    };

    private static readonly Dictionary<string, IntrinsicArraySpec> arraySpecs = new()
    {
        { "bool", new IntrinsicArraySpec(
            "bool[]",
            (f, l) => $"reader.ReadArrayBool((int){l}, ref {f});",
            f => $"writer.WriteArrayBool({f});",
            sizeof(byte)
        )},
        { "u8", new IntrinsicArraySpec(
            "byte[]",
            (f, l) => $"reader.ReadArrayU8((int){l}, ref {f});",
            f => $"writer.WriteArrayU8({f});",
            sizeof(byte)
        )},
        { "u16", new IntrinsicArraySpec(
            "ushort[]",
            (f, l) => $"reader.ReadArrayU16((int){l}, ref {f});",
            f => $"writer.WriteArrayU16({f});",
            sizeof(ushort)
        )},
        { "u32", new IntrinsicArraySpec(
            "uint[]",
            (f, l) => $"reader.ReadArrayU32((int){l}, ref {f});",
            f => $"writer.WriteArrayU32({f});",
            sizeof(uint)
        )},
        { "u64", new IntrinsicArraySpec(
            "ulong[]",
            (f, l) => $"reader.ReadArrayU64((int){l}, ref {f});",
            f => $"writer.WriteArrayU64({f});",
            sizeof(ulong)
        )},
        { "i8", new IntrinsicArraySpec(
            "sbyte[]",
            (f, l) => $"reader.ReadArrayI8((int){l}, ref {f});",
            f => $"writer.WriteArrayI8({f});",
            sizeof(sbyte)
        )},
        { "i16", new IntrinsicArraySpec(
            "short[]",
            (f, l) => $"reader.ReadArrayI16((int){l}, ref {f});",
            f => $"writer.WriteArrayI16({f});",
            sizeof(short)
        )},
        { "i32", new IntrinsicArraySpec(
            "int[]",
            (f, l) => $"reader.ReadArrayI32((int){l}, ref {f});",
            f => $"writer.WriteArrayI32({f});",
            sizeof(int)
        )},
        { "i64", new IntrinsicArraySpec(
            "long[]",
            (f, l) => $"reader.ReadArrayI64((int){l}, ref {f});",
            f => $"writer.WriteArrayI64({f});",
            sizeof(long)
        )},
        { "f32", new IntrinsicArraySpec(
            "float[]",
            (f, l) => $"reader.ReadArrayF32((int){l}, ref {f});",
            f => $"writer.WriteArrayF32({f});",
            sizeof(float)
        )},
        { "f64", new IntrinsicArraySpec(
            "double[]",
            (f, l) => $"reader.ReadArrayF64((int){l}, ref {f});",
            f => $"writer.WriteArrayF64({f});",
            sizeof(double)
        )},
        { "cstring", new IntrinsicArraySpec(
            "string[]",
            (f, l) => $"reader.ReadArrayCString((int){l}, ref {f});",
            f => $"writer.WriteArrayCString({f});",
            -1,
            f => $"ArrayIntrinsics.EstimateArrayCString({f})"
        )},
        { "wstring", new IntrinsicArraySpec(
            "string[]",
            (f, l) => $"reader.ReadArrayWString((int){l}, ref {f});",
            f => $"writer.WriteArrayWString({f});",
            -1,
            f => $"ArrayIntrinsics.EstimateArrayWString({f})"
        )},
        { "uuid", new IntrinsicArraySpec(
            "global::System.Guid[]",
            (f, l) => $"reader.ReadArrayUUID((int){l}, ref {f});",
            f => $"writer.WriteArrayUUID({f});",
            16
        )}
    };

    public static bool TryGetInrinsic(string name, [NotNullWhen(true)] out IntrinsicSpec? instrinsic) =>
        specs.TryGetValue(name, out instrinsic);
    
    public static bool TryGetArrayInrinsic(string name, [NotNullWhen(true)] out IntrinsicArraySpec? instrinsic) =>
        arraySpecs.TryGetValue(name, out instrinsic);

    public static IntrinsicSpec GetIntrinsicFromStructure(string structTypeName) => new IntrinsicSpec(
        structTypeName,
        f => $"{f}.Read(reader);",
        f => $"{f}.Write(writer);",
        -1,
        EstimateStructureSize
    );

    public static IntrinsicArraySpec GetArrayFromStructure(string structTypeName) => new IntrinsicArraySpec(
        $"{structTypeName}[]",
        (f, l) => $"reader.ReadArrayStructure((int){l}, ref {f});",
        f => $"writer.WriteArrayStructure({f});",
        -1,
        EstimateArrayStructureSize
    );

    public static string ArrayLength(string fieldName) => $"{fieldName}.Length";

    public static string EstimateStructureSize(string fieldName) => $"{fieldName}.EstimateSize()";

    public static string EstimateArrayStructureSize(string fieldName) => $"ArrayIntrinsics.EstimateArrayStructure({fieldName})";

    public static string ReadAndCast(IntrinsicSpec spec, string fieldName, string targetType) => spec.Read(fieldName).Replace(" = ", $" = ({targetType})");
}
