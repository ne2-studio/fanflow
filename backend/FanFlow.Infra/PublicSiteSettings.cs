using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public record PublicSiteSettings(string PublicHostname) : IPublicSiteSettings;
