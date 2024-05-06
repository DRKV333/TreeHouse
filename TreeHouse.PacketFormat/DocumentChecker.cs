using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TreeHouse.Common;

namespace TreeHouse.PacketFormat;

public enum CheckerErrorReason
{
    DuplicatePacketName,
    DuplicateStructureName,
    DuplicatePacketId,
    MultipleFieldDefinition,
    EmptyBranch,
    FieldTypeDifferentOnBranch,
    ReferencedFieldDoesNotExist,
    ReferencedFieldNotDefinitelyDefined,
    BranchIntegerNoCondition,
    BranchBadFieldType,
    LengthBadFieldType,
    ReferencedStructDoesNotExist,
    ReferencedPacketDoesNotExist,
    EnumTypeBadType
}

public record struct CheckerErrorSite(
    object DocumentId,
    string SiteObject,
    string SiteDetail
)
{
    public override readonly string ToString() => $"{DocumentId}#{SiteObject}#{SiteDetail}";
}

public record class DocumentCheckerError(
    CheckerErrorReason Reason,
    CheckerErrorSite Site,
    string? Related
)
{
    public override string ToString() =>
        $"{Site} " +
        Reason switch
        {
            CheckerErrorReason.DuplicatePacketName => $"A packet with this name was already defined in document {Related}.",
            CheckerErrorReason.DuplicateStructureName => $"A structure with this name was already defined in document {Related}.",
            CheckerErrorReason.DuplicatePacketId => $"The packet {Related} was defined with this same ID.",
            CheckerErrorReason.MultipleFieldDefinition => $"A field with this name was already defined at this point, possibly with a different type.",
            CheckerErrorReason.EmptyBranch => $"Branch should provide at least of isTrue or isFalse.",
            CheckerErrorReason.FieldTypeDifferentOnBranch => $"The type of the field {Related} was different on the two sides of this branch.",
            CheckerErrorReason.ReferencedFieldDoesNotExist => $"The referenced field {Related} was never defined before this point.",
            CheckerErrorReason.ReferencedFieldNotDefinitelyDefined => $"The referenced field {Related} might not be defined at this point.",
            CheckerErrorReason.BranchIntegerNoCondition => $"Branch on integer type field {Related} should specify exactly one of test_equal or test_flag.",
            CheckerErrorReason.BranchBadFieldType => $"The field {Related} can't be used in a branch, as it isn't an integer or boolean.",
            CheckerErrorReason.LengthBadFieldType => $"The field {Related} can't be used as length, as it isn't an integer.",
            CheckerErrorReason.ReferencedStructDoesNotExist => $"The referenced structure {Related} does not exist.",
            CheckerErrorReason.ReferencedPacketDoesNotExist => $"The referenced packet {Related} does not exist.",
            CheckerErrorReason.EnumTypeBadType => $"The type {Related} can't be used as the base type of an enum, as it is not an integer type.",
            _ => "Unknown error."
        };
}

public class DocumentChecker
{
    private record struct VisitorParams(
        CheckerErrorSite Site,
        Dictionary<string, FieldDefinitionInfo> Definitions
    );

    private class CheckerVisitor : FieldsListVisitor<VisitorParams>
    {
        public required DocumentChecker Checker { get; init; }

        protected override void VisitFieldItem(IFieldItem item, int index, VisitorParams param)
        {
            base.VisitFieldItem(item, index, param);
        }

        protected override void VisitField(Field field, int index, VisitorParams param)
        {
            CheckerErrorSite site = param.Site with { SiteDetail = $"{param.Site.SiteDetail}.{index}:{field.Name}" };
            base.VisitField(field, index, param with { Site = site });

            if (field.Name == null)
                return;

            FieldDefinitionInfo info = new()
            {
                Field = field,
                DefinitelyDefined = true
            };

            if (!param.Definitions.TryAddOrGet(field.Name, info, out FieldDefinitionInfo? existingInfo))
            {
                if (!existingInfo.DefinitelyDefined && IsTheSameType(field.Type, existingInfo.Field.Type))
                    existingInfo.DefinitelyDefined = true;
                else
                    Checker.Error(site, CheckerErrorReason.MultipleFieldDefinition);
            }
        }

        protected override void VisitArray(Field field, int index, ArrayFieldType type, VisitorParams param)
        {
            Checker.CheckLen(param.Site, type.Len, param.Definitions);
            Checker.CheckForStructReference(param.Site, type.Type);
            base.VisitArray(field, index, type, param);
        }

        protected override void VisitLimitedString(Field field, int index, LimitedStringFieldType type, VisitorParams param)
        {
            Checker.CheckLen(param.Site, type.Maxlen, param.Definitions);
            base.VisitLimitedString(field, index, type, param);
        }

        protected override void VisitPrimitive(Field field, int index, PrimitiveFieldType type, VisitorParams param)
        {
            Checker.CheckForStructReference(param.Site, type.Value);
            base.VisitPrimitive(field, index, type, param);
        }

        protected override void VisitEnum(Field field, int index, EnumFieldType type, VisitorParams param)
        {
            if (!IsIntegerTypeName(type.Name))
                Checker.Error(param.Site, CheckerErrorReason.EnumTypeBadType, type.Name);
            base.VisitEnum(field, index, type, param);
        }

        protected override void VisitBranch(BranchDetails branch, int index, VisitorParams param)
        {
            CheckerErrorSite site = param.Site with { SiteDetail = $"{param.Site.SiteDetail}.{index}" };

            if (branch.IsTrue == null && branch.IsFalse == null)
                Checker.Error(site, CheckerErrorReason.EmptyBranch);

            if (Checker.MakeSureIsDefinitelyDefined(site, branch.Field, param.Definitions, out FieldDefinitionInfo? branchField))
            {
                if (IsIntrinsicIntegerOrEnum(branchField.Field.Type))
                {
                    if (branch.TestFlag.HasValue == branch.TestEqual.HasValue)
                        Checker.Error(site, CheckerErrorReason.BranchIntegerNoCondition, branch.Field);
                }
                else if (!IsIntrinsicBool(branchField.Field.Type))
                {
                    Checker.Error(site, CheckerErrorReason.BranchBadFieldType, branch.Field);
                }
            }   
            
            Dictionary<string, FieldDefinitionInfo>? definitionsTrue = null;
            Dictionary<string, FieldDefinitionInfo>? definitionsFalse = null;
            
            if (branch.IsTrue != null)
            {
                CheckerErrorSite siteTrue = site with { SiteDetail = $"{site.SiteDetail}(true)" };
                definitionsTrue = param.Definitions.ToDictionary(x => x.Key, x => x.Value.Clone());
                VisitFieldsList(branch.IsTrue, param with { Site = siteTrue, Definitions = definitionsTrue});
            }

            if (branch.IsFalse != null)
            {
                CheckerErrorSite siteFalse = site with { SiteDetail = $"{site.SiteDetail}(false)" };
                definitionsFalse = param.Definitions.ToDictionary(x => x.Key, x => x.Value.Clone());
                VisitFieldsList(branch.IsFalse, param with { Site = siteFalse, Definitions = definitionsFalse });
            }

            if (definitionsTrue != null)
            {
                foreach (var (fieldName, fieldInfo) in definitionsTrue)
                {
                    if (definitionsFalse != null && definitionsFalse.TryGetValue(fieldName, out FieldDefinitionInfo? otherBranchInfo))
                    {
                        if (!IsTheSameType(fieldInfo.Field.Type, otherBranchInfo.Field.Type))
                        {
                            Checker.Error(site, CheckerErrorReason.FieldTypeDifferentOnBranch, fieldName);
                            fieldInfo.DefinitelyDefined = false;
                        }
                        else
                        {
                            fieldInfo.DefinitelyDefined &= otherBranchInfo.DefinitelyDefined;
                        }
                    }
                    else
                    {
                        fieldInfo.DefinitelyDefined = false;
                    }

                    if (!param.Definitions.TryAddOrGet(fieldName, fieldInfo, out FieldDefinitionInfo? alreadyInParent) && !alreadyInParent.DefinitelyDefined)
                    {
                        alreadyInParent.DefinitelyDefined = fieldInfo.DefinitelyDefined;
                    }
                }
            }

            if (definitionsFalse != null)
            {
                foreach (var (fieldName, fieldInfo) in definitionsFalse)
                {
                    fieldInfo.DefinitelyDefined = false;
                    param.Definitions.TryAdd(fieldName, fieldInfo);
                }
            }
        }
    }

    private sealed class FieldDefinitionInfo
    {
        public required Field Field { get; init; }
        public bool DefinitelyDefined { get; set; }

        public FieldDefinitionInfo Clone() => (FieldDefinitionInfo)MemberwiseClone();
    }

    private readonly List<DocumentCheckerError> errors = new();

    private readonly Dictionary<string, object> definedPackets = new();
    private readonly Dictionary<string, object> definedStructures = new();

    private readonly Dictionary<(int id, int subId), string> packetsById = new();

    private readonly List<DocumentCheckerError> referencedStructs = new();
    private readonly List<DocumentCheckerError> referencedPackets = new();

    public IEnumerable<DocumentCheckerError> Errors => errors;

    public void CheckDocument(object documentId, PacketFormatDocument document)
    {
        foreach (var (packetName, packet) in document.Packets)
        {
            CheckerErrorSite site = new(documentId, packetName, "");

            if (packet.Inherit != null)
                ReferencePacket(site, packet.Inherit);

            if (!definedPackets.TryAddOrGet(packetName, documentId, out object? existingDoc))
                Error(site, CheckerErrorReason.DuplicatePacketName, existingDoc.ToString());

            if (!(packet.Id == 0 && packet.SubId == 0))
            {
                if (!packetsById.TryAddOrGet((packet.Id, packet.SubId), packetName, out string? existingPacket))
                    Error(site, CheckerErrorReason.DuplicatePacketId, existingPacket);
            }

            CheckFields(site, packet, new());
        }

        foreach (var (structName, aStruct) in document.Structures)
        {
            CheckerErrorSite site = new(documentId, structName, "");

            if (!definedStructures.TryAddOrGet(structName, documentId, out object? existingDoc))
                Error(site, CheckerErrorReason.DuplicateStructureName, existingDoc.ToString());

            CheckFields(site, aStruct, new());
        }
    }

    public void CheckReferences()
    {
        errors.AddRange(referencedPackets.Where(x => !definedPackets.ContainsKey(x.Related!)));
        referencedPackets.Clear();
        errors.AddRange(referencedStructs.Where(x => !definedStructures.ContainsKey(x.Related!)));
        referencedStructs.Clear();
    }

    private void CheckFields(CheckerErrorSite parentSite, FieldsList fields, Dictionary<string, FieldDefinitionInfo> definitions)
    {
        CheckerVisitor visitor = new()
        {
            Checker = this
        };

        visitor.VisitFieldsList(fields, new VisitorParams(parentSite, definitions));
    }

    private void CheckLen(CheckerErrorSite site, string len, Dictionary<string, FieldDefinitionInfo> definitions)
    {
        if (!IsDigitsOnly(len))
        {
            if (MakeSureIsDefinitelyDefined(site, len, definitions, out FieldDefinitionInfo? arrayLenField) &&
                !IsIntrinsicIntegerOrEnum(arrayLenField.Field.Type))
            {
                Error(site, CheckerErrorReason.LengthBadFieldType, len);
            }
        }
    }

    private bool MakeSureIsDefinitelyDefined(CheckerErrorSite site, string field, Dictionary<string, FieldDefinitionInfo> definitions, [NotNullWhen(true)] out FieldDefinitionInfo? fieldInfo)
    {
        if (!definitions.TryGetValue(field, out fieldInfo))
        {
            Error(site, CheckerErrorReason.ReferencedFieldDoesNotExist, field);
            return false;
        }
        else
        {
            if (!fieldInfo.DefinitelyDefined)
            {
                Error(site, CheckerErrorReason.ReferencedFieldNotDefinitelyDefined, field);
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    private static bool IsTheSameType(IFieldType first, IFieldType second) => first switch
    {
        PrimitiveFieldType primitive => second is PrimitiveFieldType primitiveSecond && primitive.Value == primitiveSecond.Value,
        LimitedStringFieldType lim => second is LimitedStringFieldType limSecond && lim.Name == limSecond.Name,
        ArrayFieldType array => second is ArrayFieldType arraySecond && array.Name == arraySecond.Name && array.Type == arraySecond.Type,
        EnumFieldType enumFirst => second is EnumFieldType enumSecond && enumFirst.Name == enumSecond.Name,
        _ => false
    };

    private static bool IsIntrinsicIntegerOrEnum(IFieldType type) =>
        type is EnumFieldType || type is PrimitiveFieldType primitive && IsIntegerTypeName(primitive.Value);

    private static bool IsIntegerTypeName(string type) =>
        type is "u8" or "u16" or "u32" or "u64" or "i8" or "i16" or "i32" or "i64";

    private static bool IsIntrinsicBool(IFieldType type) =>
        type is PrimitiveFieldType primitive && primitive.Value is "bool";

    private static bool IsDigitsOnly(string str) => str.All(char.IsAsciiDigit);

    private void Error(CheckerErrorSite site, CheckerErrorReason reason, string? related = null) =>
        errors.Add(new DocumentCheckerError(reason, site, related));

    private void ReferencePacket(CheckerErrorSite site, string packet) =>
        referencedPackets.Add(new DocumentCheckerError(CheckerErrorReason.ReferencedPacketDoesNotExist, site, packet));

    private void CheckForStructReference(CheckerErrorSite site, string aStruct)
    {
        if (aStruct.StartsWith(':'))
            referencedStructs.Add(new DocumentCheckerError(CheckerErrorReason.ReferencedStructDoesNotExist, site, aStruct[1..]));
    }
}
