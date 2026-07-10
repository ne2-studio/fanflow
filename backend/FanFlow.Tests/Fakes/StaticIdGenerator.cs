using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticIdGenerator(Guid id) : IIdGenerator
{
    public Guid NewId()
    {
        return id;
    }
}
