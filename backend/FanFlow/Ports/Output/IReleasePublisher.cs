namespace FanFlow.Ports.Output;

/// <summary>
/// Drives the static landing page pipeline (Database -> Static Generator -> HTML -> Shared Volume -> Nginx).
/// </summary>
public interface IReleasePublisher
{
    Task PublishAsync(Release release);
    Task UnpublishAsync(string slug);
}
