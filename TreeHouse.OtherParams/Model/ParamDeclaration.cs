using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeHouse.OtherParams.Model;

public class ParamDeclaration
{
    [Key]
    public int ParamId { get; set; }

    [ForeignKey(nameof(Class))]
    public int ClassId { get; set; }

    [InverseProperty(nameof(Model.Class.DeclaredParams))]
    public required Class Class { get; set; }

    [ForeignKey(nameof(Definition))]
    public int DefinitionId { get; set; }

    [InverseProperty(nameof(Param.Declarations))]
    public required Param Definition { get; set; }
}
