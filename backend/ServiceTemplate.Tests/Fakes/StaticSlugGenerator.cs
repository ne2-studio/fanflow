using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Tests.Fakes;

public class StaticSlugGenerator : ISlugGenerator
{
    public string Generate(string title) => title.Trim().ToLowerInvariant().Replace(' ', '-');
}
