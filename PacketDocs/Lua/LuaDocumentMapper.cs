using System.Collections.Generic;
using System.Linq;
using Common;
using PacketFormat;

namespace PacketDocs.Lua;

internal class LuaDocumentMapper
{
    private readonly List<StructureFieldType> structsToIndex = new();

    private readonly Dictionary<string, int> packetIndexes = new();
    
    private readonly Dictionary<string, int> structureIndexes = new();

    public LuaPacketFormatDocument LuaDocument { get; } = new();

    public void AddDocument(PacketFormatDocument document)
    {
        foreach (var packet in document.Packets)
        {
            LuaDocument.Packets.Add(new LuaPacketDefinition()
            {
                Original = packet.Value,
                Name = packet.Key,
                Inherit = packet.Value.Inherit == null ? null : -1, 
                Fields = MapFieldItems(packet.Value.Fields)
            });

            int index = LuaDocument.Packets.Count;

            packetIndexes.Add(packet.Key, index);

            if (!(packet.Value.Id == 0 && packet.Value.SubId == 0))
            {
                Dictionary<int, int> idDict = LuaDocument.ById.TryGetOrAdd(packet.Value.Id, x => new Dictionary<int, int>());
                idDict.Add(packet.Value.SubId, index);
            }
        }

        foreach (var structure in document.Structures)
        {
            LuaDocument.Structures.Add(new NamedFieldsList()
            {
                Name = structure.Key,
                Fields = MapFieldItems(structure.Value.Fields)
            });
            structureIndexes.Add(structure.Key, LuaDocument.Structures.Count);
        }
    }

    public void SetIndexes()
    {
        foreach (LuaPacketDefinition packet in LuaDocument.Packets)
        {
            if (packet.Inherit == -1 && packet.Original.Inherit != null)
                packet.Inherit = packetIndexes[packet.Original.Inherit];
        }

        foreach (StructureFieldType type in structsToIndex)
        {
            type.Index = structureIndexes[type.Name];
        }
        structsToIndex.Clear();
    }

    private List<IFieldItem> MapFieldItems(IEnumerable<IFieldItem> fields) => fields.Select(MapLuaFieldItem).ToList();

    private IFieldItem MapLuaFieldItem(IFieldItem item)
    {
        if (item is Field field)
        {
            return new Field()
            {
                Name = field.Name,
                Type = MapFieldType(field.Type)
            };
        }
        else if (item is Branch branch)
        {
            return new Branch()
            {
                Details = new BranchDetails()
                {
                    Field = branch.Details.Field,
                    TestEqual = branch.Details.TestEqual,
                    TestFlag = branch.Details.TestFlag,
                    IsTrue = branch.Details.IsTrue == null ? null : new FieldsList { Fields = MapFieldItems(branch.Details.IsTrue.Fields) },
                    IsFalse = branch.Details.IsFalse == null ? null : new FieldsList { Fields = MapFieldItems(branch.Details.IsFalse.Fields) },
                }
            };
        }
        else
        {
            return item;
        }
    }

    private IFieldType MapFieldType(IFieldType type)
    {
        if (type is ArrayFieldType arrayType)
        {
            return new LuaArrayFieldType()
            {
                Len = ParseIntMaybe(arrayType.Len),
                Type = MapPrimitiveStructureReference(new PrimitiveFieldType() { Value = arrayType.Type })
            };
        }
        else if (type is LimitedStringFieldType limitedStringType)
        {
            return new LuaLimitedStringFieldType()
            {
                Name = limitedStringType.Name,
                Maxlen = ParseIntMaybe(limitedStringType.Maxlen)
            };
        }
        else if (type is PrimitiveFieldType primitive)
        {
            return MapPrimitiveStructureReference(primitive);
        }
        else
        {
            return type;
        }
    }

    private IFieldType MapPrimitiveStructureReference(PrimitiveFieldType primitive)
    {
        if (primitive.Value.StartsWith(':'))
        {
            StructureFieldType type = new()
            {
                Name = primitive.Value[1..]
            };
            structsToIndex.Add(type);
            return type;
        }
        else
        {
            return primitive;
        }
    }

    private static object ParseIntMaybe(string str)
    {
        if (int.TryParse(str, out int number))
            return number;
        return str;
    }
}
