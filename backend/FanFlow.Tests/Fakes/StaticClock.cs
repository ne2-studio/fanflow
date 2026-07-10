using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticClock : IClock
{
    private readonly DateTime utcNow = DateTime.UtcNow;

    public DateTime UtcNow()
    {
        return utcNow;
    }
}
