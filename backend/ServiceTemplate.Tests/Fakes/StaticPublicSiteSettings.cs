using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Tests.Fakes;

public class StaticPublicSiteSettings(string publicHostname = "fanflow.app") : IPublicSiteSettings
{
    public string PublicHostname { get; } = publicHostname;
}
