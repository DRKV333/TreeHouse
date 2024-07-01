using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeHouse.OtherParams.Model;

public class ParamDefinition
{
    [Key]
    public int Id { get; set; }

    public required string Name { get; set; }

    [ForeignKey(nameof(Type))]
    public ParamType TypeId { get; set; }

    [InverseProperty(nameof(ParamTypeName.Definitions))]
    public required ParamTypeName Type { get; set; }

    public string? Default { get; set; }

    public float? Priority { get; set; }

    public float? Tg { get; set; }

    public string? EditType { get; set; }

    public string? Group { get; set; }

    public string? EngineBindingName { get; set; }

    public required string ConstraintParam { get; set; }

    public string? Help { get; set; }

    public bool Persistent { get; set; }

    public bool Content { get; set; }

    public bool PerInstanceSetting { get; set; }

    public bool NodeOwn { get; set; }

    public bool Deprecated { get; set; }

    public bool ServerOwn { get; set; }

    public bool ExcludeFromClient { get; set; }

    public bool DupeSetOk { get; set; }

    public bool ClientUnknown { get; set; }

    public bool Metric { get; set; }

    public bool ClientOwn { get; set; }

    public bool EquipSlot { get; set; }

    public bool ClientPrivileged { get; set; }

    public bool Uts { get; set; }

    public bool ClientInit { get; set; }

    [ForeignKey(nameof(DefinedIn))]
    public int DefinedInId { get; set; }

    [InverseProperty(nameof(Class.DefinedParams))]
    public required Class DefinedIn { get; set; }

    [InverseProperty(nameof(ParamDeclaration.Definition))]
    public ICollection<ParamDeclaration> Declarations { get; set; } = new List<ParamDeclaration>();

    [ForeignKey(nameof(Overrides))]
    public int? OverridesId { get; set; }

    public ParamDefinition? Overrides { get; set; }
}
