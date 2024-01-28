using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Common;

namespace PacketFormat;

public enum CheckerErrorReason
{
    DuplicatePacketName,
    DuplicateStructureName,
    DuplicatePacketId,
    MultipleFieldDefinition,
    EmptyBranch,
    FieldTypeDifferentOnBranch,
    ReferencedFieldDoesNotExist,
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
);

public record class DocumentCheckerError(
    CheckerErrorReason Reason,
    CheckerErrorSite Site,
    string? Related
)
{
    public string ToErrorMessage() => // TODO
        $"{Site.DocumentId}: " +
        Reason switch
        {
            _ => "Unknown error."
        };
}

public class DocumentChecker
{
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
        foreach (var (item, index) in fields.Fields.WithIndex())
        {
            if (item is Field field)
            {
                CheckerErrorSite site = parentSite with { SiteDetail = $"{parentSite.SiteDetail}.{index}:{field.Name}" };

                if (field.Type is ArrayFieldType arrayType)
                {
                    CheckLen(site, arrayType.Len, definitions);
                    CheckForStructReference(site, arrayType.Type);
                }
                else if (field.Type is LimitedStringFieldType limitedStringType)
                {
                    CheckLen(site, limitedStringType.Maxlen, definitions);
                }
                else if (field.Type is PrimitiveFieldType primitive)
                {
                    CheckForStructReference(site, primitive.Value);
                }
                else if (field.Type is EnumFieldType enumType)
                {
                    if (!IsIntegerTypeName(enumType.Name))
                        Error(site, CheckerErrorReason.EnumTypeBadType, enumType.Name);
                }

                if (field.Name == null)
                    continue;

                FieldDefinitionInfo info = new FieldDefinitionInfo()
                {
                    Field = field,
                    DefinitelyDefined = true
                };

                if (!definitions.TryAddOrGet(field.Name, info, out FieldDefinitionInfo? existingInfo))
                {
                    if (!existingInfo.DefinitelyDefined && IsTheSameType(field.Type, existingInfo.Field.Type))
                        existingInfo.DefinitelyDefined = true;
                    else
                        Error(site, CheckerErrorReason.MultipleFieldDefinition);
                }
            }
            else if (item is Branch branch)
            {
                CheckerErrorSite site = parentSite with { SiteDetail = $"{parentSite.SiteDetail}.{index}" };

                if (branch.Details.IsTrue == null && branch.Details.IsFalse == null)
                    Error(site, CheckerErrorReason.EmptyBranch);

                if (!IsDefinitelyDefined(branch.Details.Field, definitions, out FieldDefinitionInfo? branchField))
                {
                    Error(site, CheckerErrorReason.ReferencedFieldDoesNotExist, branch.Details.Field);
                }
                else
                {
                    if (IsIntrinsicIntegerOrEnum(branchField.Field.Type))
                    {
                        if (branch.Details.TestFlag.HasValue == branch.Details.TestEqual.HasValue)
                            Error(site, CheckerErrorReason.BranchIntegerNoCondition, branch.Details.Field);
                    }
                    else if (!IsIntrinsicBool(branchField.Field.Type))
                    {
                        Error(site, CheckerErrorReason.BranchBadFieldType, branch.Details.Field);
                    }
                }   
                
                Dictionary<string, FieldDefinitionInfo>? definitionsTrue = null;
                Dictionary<string, FieldDefinitionInfo>? definitionsFalse = null;
                
                if (branch.Details.IsTrue != null)
                {
                    CheckerErrorSite siteTrue = site with { SiteDetail = $"{site.SiteDetail}(true)" };
                    definitionsTrue = definitions.ToDictionary(x => x.Key, x => x.Value.Clone());
                    CheckFields(siteTrue, branch.Details.IsTrue, definitionsTrue);
                }

                if (branch.Details.IsFalse != null)
                {
                    CheckerErrorSite siteFalse = site with { SiteDetail = $"{site.SiteDetail}(false)" };
                    definitionsFalse = definitions.ToDictionary(x => x.Key, x => x.Value.Clone());
                    CheckFields(siteFalse, branch.Details.IsFalse, definitionsFalse);
                }

                if (definitionsTrue != null)
                {
                    foreach (var (fieldName, fieldInfo) in definitionsTrue)
                    {
                        if (definitionsFalse != null)
                        {
                            if (definitionsFalse.TryGetValue(fieldName, out FieldDefinitionInfo? otherBranchInfo))
                            {
                                if (!IsTheSameType(fieldInfo.Field.Type, otherBranchInfo.Field.Type))
                                {
                                    Error(site, CheckerErrorReason.FieldTypeDifferentOnBranch, fieldName);
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
                        }
                        
                        if (!definitions.TryAddOrGet(fieldName, fieldInfo, out FieldDefinitionInfo? alreadyInParent))
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
                        definitions.TryAdd(fieldName, fieldInfo);
                    }
                }
            }
        }
    }

    private void CheckLen(CheckerErrorSite site, string len, Dictionary<string, FieldDefinitionInfo> definitions)
    {
        if (!IsDigitsOnly(len))
        {
            if (!IsDefinitelyDefined(len, definitions, out FieldDefinitionInfo? arrayLenField))
                Error(site, CheckerErrorReason.ReferencedFieldDoesNotExist, len);
            else if (!IsIntrinsicIntegerOrEnum(arrayLenField.Field.Type))
                Error(site, CheckerErrorReason.LengthBadFieldType, len);
        }
    }

    private static bool IsDefinitelyDefined(string field, Dictionary<string, FieldDefinitionInfo> definitions, [NotNullWhen(true)] out FieldDefinitionInfo? fieldInfo) =>
        definitions.TryGetValue(field, out fieldInfo) && fieldInfo.DefinitelyDefined;

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
