using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeHouse.OtherParams.Model;

public class Class
{
    [Key]
    public int UniqueId { get; set; }

    public required string Name { get; set; }

    public string? BindsTo { get; set; }

    [InverseProperty(nameof(Table.Classes))]
    public required Table ContentTableBinding { get; set; }

    [ForeignKey(nameof(Extends))]
    public int? ExtendsId { get; set; }

    [InverseProperty(nameof(Descendants))]
    public Class? Extends { get; set; }

    [InverseProperty(nameof(Extends))]
    public ICollection<Class> Descendants { get; set; } = new List<Class>();

    public string? Icon { get; set; }

    [InverseProperty(nameof(Param.DefinedIn))]
    public ICollection<Param> DefinedParams { get; set; } = new List<Param>();

    [InverseProperty(nameof(ParamDeclaration.Class))]
    public ICollection<ParamDeclaration> DeclaredParams { get; set; } = new List<ParamDeclaration>();
}
