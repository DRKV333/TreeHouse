using TreeHouse.Common.IO;

namespace TreeHouse.PacketParser.Support;

public interface ISpanReadWrite
{
    void Read(SpanReader reader);

    int EstimateSize();

    void Write(SpanWriter writer);
}
