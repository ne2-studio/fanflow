using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class StaticSlugGenerator : ISlugGenerator
{
    public string Generate(string title) => title.Trim().ToLowerInvariant().Replace(' ', '-');
}
