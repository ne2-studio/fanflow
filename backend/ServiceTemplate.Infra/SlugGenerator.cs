using System.Text.RegularExpressions;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Infra;

public partial class SlugGenerator : ISlugGenerator
{
    public string Generate(string title)
    {
        var lowercase = title.Trim().ToLowerInvariant();
        var withHyphens = NonAlphanumeric().Replace(lowercase, "-");
        var collapsed = RepeatedHyphens().Replace(withHyphens, "-").Trim('-');

        return string.IsNullOrEmpty(collapsed) ? "release" : collapsed;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex RepeatedHyphens();
}
