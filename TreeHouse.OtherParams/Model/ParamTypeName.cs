using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeHouse.OtherParams.Model;

public class ParamTypeName
{
    [Key]
    public ParamType Id { get; set; }

    public required string Name { get; set; }

    [InverseProperty(nameof(ParamDefinition.Type))]
    public ICollection<ParamDefinition> Definitions { get; set; } = new List<ParamDefinition>();
}
