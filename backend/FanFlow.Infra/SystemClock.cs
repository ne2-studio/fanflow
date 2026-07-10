using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public class SystemClock : IClock
{
    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }
}
