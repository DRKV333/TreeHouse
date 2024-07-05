using System;

namespace TreeHouse.OtherParams.JsonConverter;

public class ParamParsingException : Exception
{
    public int FieldNum { get; }
    public ushort ParamId { get; }
    public ParamType TypeId { get; }
    public int Offset { get; }

    public ParamParsingException(int FieldNum, ushort ParamId, ParamType TypeId, int Offset, string? message = null, Exception? inner = null) : base(message, inner)
    {
        this.FieldNum = FieldNum;
        this.ParamId = ParamId;
        this.TypeId = TypeId;
        this.Offset = Offset;
    }

    public override string Message => $"Failed to parse field {FieldNum} param {ParamId:X4}, of type {TypeId}, at offset 0x{Offset:X}: {base.Message}";
}
