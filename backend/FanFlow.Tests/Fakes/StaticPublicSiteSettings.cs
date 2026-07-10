using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticPublicSiteSettings(string publicHostname = "fanflow.app") : IPublicSiteSettings
{
    public string PublicHostname { get; } = publicHostname;
}
