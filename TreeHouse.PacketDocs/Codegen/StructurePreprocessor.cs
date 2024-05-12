using System.Collections.Generic;
using TreeHouse.PacketFormat;

namespace TreeHouse.PacketDocs.Codegen;

internal class StructurePreprocessor
{
    private class PreprocessorVisitor : FieldsListVisitor<Dictionary<string, int>>
    {
        protected override void VisitStruct(Field field, int index, string type, Dictionary<string, int> param)
        {
            NameField(field, type, param);
            base.VisitStruct(field, index, type, param);
        }

        protected override void VisitArray(Field field, int index, ArrayFieldType type, Dictionary<string, int> param)
        {
            if (type.Type.StartsWith(':'))
                NameField(field, type.Type[1..], param);
            base.VisitArray(field, index, type, param);
        }

        private void NameField(Field field, string type, Dictionary<string, int> unnamedStructureCounts)
        {
            if (field.Name == null)
            {
                if (!unnamedStructureCounts.TryGetValue(type, out int count))
                    count = 1;

                field.Name = $"{StructureBuilder.ConvertTypeName(type)}{count}";

                unnamedStructureCounts[type] = count + 1;
            }
        }
    }

    private readonly PreprocessorVisitor visitor = new();

    public void Preprocess(FieldsList fieldsList)
    {
        visitor.VisitFieldsList(fieldsList, new Dictionary<string, int>());
    }
}
