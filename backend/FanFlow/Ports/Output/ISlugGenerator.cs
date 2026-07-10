namespace FanFlow.Ports.Output;

/// <summary>
/// Derives a URL-safe, human-readable slug from a release title (e.g. "Run To Me" -> "run-to-me").
/// Uniqueness is checked/resolved by the caller via IReleaseRepository, not by this generator.
/// </summary>
public interface ISlugGenerator
{
    string Generate(string title);
}
