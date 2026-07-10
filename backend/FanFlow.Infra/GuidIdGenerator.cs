using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public class GuidIdGenerator : IIdGenerator
{
    public Guid NewId()
    {
        return Guid.NewGuid();
    }
}
