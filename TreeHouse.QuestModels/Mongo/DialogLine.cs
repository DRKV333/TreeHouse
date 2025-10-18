using System;

namespace TreeHouse.QuestModels.Mongo;

public class DialogLine
{
    [Flags]
    public enum Response
    {
        None = 0,
        Yes = 1,
        No = 2,
        More = 4,
        Broken = 8
    }

    public long Id { get; set; }

    public Response Responses { get; set; }
}
