using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class SpyReleasePublisher : IReleasePublisher
{
    public List<Release> PublishedReleases { get; } = new();
    public List<string> UnpublishedSlugs { get; } = new();

    public Task PublishAsync(Release release)
    {
        PublishedReleases.Add(release);
        return Task.CompletedTask;
    }

    public Task UnpublishAsync(string slug)
    {
        UnpublishedSlugs.Add(slug);
        return Task.CompletedTask;
    }
}
