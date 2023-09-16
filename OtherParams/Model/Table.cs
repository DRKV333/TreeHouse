using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OtherParams.Model;

public class Table
{
    [Key]
    public int Id { get; set; }

    public required string Name { get; set; }

    [InverseProperty(nameof(Class.ContentTableBinding))]
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
