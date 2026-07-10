using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Infra;

public record PublicSiteSettings(string PublicHostname) : IPublicSiteSettings;
