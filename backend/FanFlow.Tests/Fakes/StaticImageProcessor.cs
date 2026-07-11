using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticImageProcessor : IImageProcessor
{
    public List<byte[]> ProcessedSources { get; } = new();

    public byte[] ToSquareWebp(byte[] source, int size)
    {
        ProcessedSources.Add(source);
        return "processed"u8.ToArray();
    }
}
