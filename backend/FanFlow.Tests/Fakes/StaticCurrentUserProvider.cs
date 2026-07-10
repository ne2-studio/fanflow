using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticCurrentUserProvider(string userId) : ICurrentUserProvider
{
    public string GetUserId()
    {
        return userId;
    }
}
