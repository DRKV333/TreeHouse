using System.Collections.Generic;
using TreeHouse.Common;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Lua;

internal class LuaDocumentMapper
{
    private sealed class LuaFieldsListMapper : FieldsListVisitor<IList<IFieldItem>>
    {
        public required LuaDocumentMapper Mapper { get; init; }

        public required string ListName { get; init; }
        
        private readonly Dictionary<string, (LuaField def, int index)> fieldDefs = new();

        private int nextStash = 1;

        private int nextBranch = 1;

        int unnamedCounter;

        public override void VisitFieldsList(FieldsList fieldsList, IList<IFieldItem> param)
        {
            unnamedCounter = 1;
            base.VisitFieldsList(fieldsList, param);
        }

        protected override void VisitField(Field field, int index, IList<IFieldItem> param)
        {
            (LuaField _, int luaIndex) = fieldDefs.TryGetOrAdd(field.Name ?? $"unnamed{unnamedCounter++}", name => {
                string abbrev = $"{ListName}.{name}";
                LuaField def = new()
                {
                    Name = FieldToDisplayName(field),
                    Abbrev = abbrev
                };
                Mapper.MapFieldType(def, field.Type);

                Mapper.LuaDocument.FieldDefinitions.Add(def);
                int index = Mapper.LuaDocument.FieldDefinitions.Count;

                return (def, index);
            });

            base.VisitField(field, luaIndex, param);
        }

        protected override void VisitArray(Field field, int index, ArrayFieldType type, IList<IFieldItem> param)
        {
            param.Add(new LuaFieldWithLengthOverride() { Index = index, Len = MapLen(type.Len) });
            base.VisitArray(field, index, type, param);
        }

        protected override void VisitLimitedString(Field field, int index, LimitedStringFieldType type, IList<IFieldItem> param)
        {
            param.Add(new LuaFieldWithLengthOverride() { Index = index, Len = MapLen(type.Maxlen) });
            base.VisitLimitedString(field, index, type, param);
        }

        protected override void VisitEnum(Field field, int index, EnumFieldType type, IList<IFieldItem> param)
        {
            param.Add(new LuaFieldIndex() { Index = index });
            base.VisitEnum(field, index, type, param);
        }

        protected override void VisitPrimitive(Field field, int index, PrimitiveFieldType type, IList<IFieldItem> param)
        {
            param.Add(new LuaFieldIndex() { Index = index });
            base.VisitPrimitive(field, index, type, param);
        }

        protected override void VisitBranch(BranchDetails branch, int index, IList<IFieldItem> param)
        {
            Mapper.LuaDocument.Branches.Add(new LuaBranchDescription()
            {
                Abbrev = $"{ListName}.branch{nextBranch++}",
                Name = BranchToDisplay(branch),
                FieldIndex = MapStash(branch.Field),
                TestEqual = branch.TestEqual,
                TestFlag = branch.TestFlag,
            });

            param.Add(new LuaBranch()
            {
                Details = new LuaBranchDetails()
                {
                    Index = Mapper.LuaDocument.Branches.Count,
                    IsTrue = branch.IsTrue == null ? null : new FieldsList { Fields = MapFieldItems(branch.IsTrue) },
                    IsFalse = branch.IsFalse == null ? null : new FieldsList { Fields = MapFieldItems(branch.IsFalse) },
                }
            });
        }

        public List<IFieldItem> MapFieldItems(FieldsList fields)
        {
            List<IFieldItem> mappedFields = new();
            VisitFieldsList(fields, mappedFields);
            return mappedFields;
        }

        private static string FieldToDisplayName(Field field)
        {
            if (field.Name == null)
                return TypeToDisplayName(field.Type);
            else if (field.Type is ArrayFieldType or LimitedStringFieldType)
                return $"{field.Name} {TypeToDisplayName(field.Type)}";
            else
                return field.Name;
        }

        private static string TypeToDisplayName(IFieldType type)
        {
            if (type is PrimitiveFieldType primitive)
                return primitive.Value;
            else if (type is ArrayFieldType array)
                return $"{array.Type}[{array.Len}]";
            else if (type is LimitedStringFieldType limited)
                return $"{limited.Name}[{limited.Maxlen}]";
            else
                return "???";
        }

        private static string BranchToDisplay(BranchDetails details)
        {
            if (details.TestEqual != null)
                return $"{details.Field} == {details.TestEqual}";
            else if (details.TestFlag != null)
                return $"{details.Field} & {details.TestFlag:X}";
            else
                return details.Field;
        }

        private int MapLen(string len)
        {
            if (int.TryParse(len, out int lenNum))
                return lenNum;
            else
                return MapStash(len) * -1;
        }

        private int MapStash(string field)
        {
            (LuaField def, int _) = fieldDefs[field];
            if (def.Stash == null)
                def.Stash = nextStash++;
            return def.Stash.Value;
        }
    }

    private readonly bool singleByteIds;

    private readonly List<StructureFieldType> structsToIndex = new();

    private readonly Dictionary<string, int> packetIndexes = new();
    
    private readonly Dictionary<string, int> structureIndexes = new();

    public LuaPacketFormatDocument LuaDocument { get; } = new();

    public LuaDocumentMapper(bool singleByteIds = false)
    {
        this.singleByteIds = singleByteIds;
        LuaDocument.IdLength = singleByteIds ? 1 : 2;
    }

    public void AddDocument(PacketFormatDocument document)
    {
        foreach (var packet in document.Packets)
        {
            LuaFieldsListMapper fieldsMapper = new()
            {
                Mapper = this,
                ListName = packet.Key
            };

            LuaDocument.Packets.Add(new LuaPacketDefinition()
            {
                InheritName = packet.Value.Inherit,
                Name = packet.Key,
                Inherit = packet.Value.Inherit == null ? null : -1,
                Fields = fieldsMapper.MapFieldItems(packet.Value)
            });

            int index = LuaDocument.Packets.Count;

            packetIndexes.Add(packet.Key, index);

            if (!(packet.Value.Id == 0 && packet.Value.SubId == 0))
            {
                if (singleByteIds)
                {
                    LuaDocument.ById.Add(packet.Value.Id, index);
                }
                else
                {
                    Dictionary<int, int> idDict = (Dictionary<int, int>)LuaDocument.ById.TryGetOrAdd(packet.Value.Id, x => new Dictionary<int, int>());
                    idDict.Add(packet.Value.SubId, index);
                }
            }
        }

        foreach (var structure in document.Structures)
        {
            LuaFieldsListMapper fieldsMapper = new()
            {
                Mapper = this,
                ListName = structure.Key
            };

            LuaDocument.Structures.Add(new NamedFieldsList()
            {
                Name = structure.Key,
                Fields = fieldsMapper.MapFieldItems(structure.Value)
            });
            structureIndexes.Add(structure.Key, LuaDocument.Structures.Count);
        }
    }

    public void SetIndexes()
    {
        foreach (LuaPacketDefinition packet in LuaDocument.Packets)
        {
            if (packet.Inherit == -1 && packet.InheritName != null)
                packet.Inherit = packetIndexes[packet.InheritName];
        }

        foreach (StructureFieldType type in structsToIndex)
        {
            type.Index = structureIndexes[type.Name];
        }
        structsToIndex.Clear();
    }

    private void MapFieldType(LuaField field, IFieldType type)
    {
        if (type is ArrayFieldType arrayType)
        {
            if (arrayType.Type is "u8" or "i8")
            {
                field.Type = new LuaArrayFieldType()
                {
                    Items = -1
                };
            }
            else
            {
                LuaDocument.FieldDefinitions.Add(new LuaField() {
                    Name = field.Name + " Item",
                    Abbrev = field.Abbrev + ".item",
                    Type = MapPrimitiveStructureReference(new PrimitiveFieldType() { Value = arrayType.Type })
                });

                field.Type = new LuaArrayFieldType()
                {
                    Items = LuaDocument.FieldDefinitions.Count
                };
            }
        }
        else if (type is LimitedStringFieldType limitedStringType)
        {
            field.Type = new PrimitiveFieldType()
            {
                Value = limitedStringType.Name
            };
        }
        else if (type is PrimitiveFieldType primitive)
        {
            field.Type = MapPrimitiveStructureReference(primitive);
        }
        else
        {
            field.Type = type;
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
}
