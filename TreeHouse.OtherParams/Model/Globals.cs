using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeHouse.OtherParams.Model;

public class Globals
{
    [Key]
    public int Id { get; set; }

    public int DataVer { get; set; }

    [ForeignKey(nameof(DefaultClientAvatarClass))]
    public int DefaultClientAvatarClassId { get; set; }
    
    public required Class DefaultClientAvatarClass { get; set; }

    [ForeignKey(nameof(DefaultPartyClass))]
    public int DefaultPartyClassId { get; set; }

    public required Class DefaultPartyClass { get; set; }

    [ForeignKey(nameof(DefaultTradeClass))]
    public int DefaultTradeClassId { get; set; }

    public required Class DefaultTradeClass { get; set; }

    [ForeignKey(nameof(DefaultMailClass))]
    public int DefaultMailClassId { get; set; }

    public required Class DefaultMailClass { get; set; }

    [ForeignKey(nameof(DefaultClanClass))]
    public int DefaultClanClassId { get; set; }
    
    public required Class DefaultClanClass { get; set; }
}
