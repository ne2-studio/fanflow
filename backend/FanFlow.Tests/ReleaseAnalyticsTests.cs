using Microsoft.Extensions.Logging.Abstractions;
using FanFlow.Application;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;
using FanFlow.Tests.Fakes;

namespace FanFlow.Tests;

public class ReleaseAnalyticsTests
{
    private const string TenantId = "user-1";

    private readonly InMemoryReleaseRepository releaseRepository;
    private readonly InMemoryEventRepository eventRepository;
    private readonly ReleaseAnalytics releaseAnalytics;
    private readonly Release release;

    public ReleaseAnalyticsTests()
    {
        releaseRepository = new InMemoryReleaseRepository();
        eventRepository = new InMemoryEventRepository();
        releaseAnalytics = new ReleaseAnalytics(
            NullLogger<ReleaseAnalytics>.Instance, releaseRepository, eventRepository, new StaticCurrentUserProvider(TenantId));

        release = new Release(
            Guid.NewGuid(), TenantId, "run-to-me", "Run To Me", "headline", "desc", "cover", "bg", "Listen", "123456789012345",
            [new DestinationLink("Spotify", "https://open.spotify.com/x")], ReleaseStatus.Published, DateTime.UtcNow, DateTime.UtcNow);
        releaseRepository.SaveAsync(release).Wait();
    }

    private TrackedEvent PageView(EventClassification? classification, string? country = "US") => new(
        Guid.NewGuid(), release.Id, EventType.PageView, "1.2.3.4", "Mozilla/5.0", null, null, null, country,
        classification == null ? null : 10, classification, DateTime.UtcNow);

    private TrackedEvent Click(EventClassification? classification) => new(
        Guid.NewGuid(), release.Id, EventType.DestinationClick, "1.2.3.4", "Mozilla/5.0", null, "spotify", 5000, "US",
        classification == null ? null : 10, classification, DateTime.UtcNow);

    [Fact]
    public async Task GetAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await releaseAnalytics.GetAsync(Guid.NewGuid().ToString(), TrafficFilter.All);

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task GetAsync_ShouldCountAllTraffic_WhenFilterIsAll()
    {
        await eventRepository.SaveAsync(PageView(EventClassification.Human));
        await eventRepository.SaveAsync(PageView(EventClassification.Bot));
        await eventRepository.SaveAsync(PageView(null));
        await eventRepository.SaveAsync(Click(EventClassification.Human));

        var result = await releaseAnalytics.GetAsync(release.Id.ToString(), TrafficFilter.All);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Views);
        Assert.Equal(1, result.Value.QualifiedViews);
        Assert.Equal(1, result.Value.Clicks);
        Assert.Equal(1.0 / 3.0, result.Value.Ctr, 3);
    }

    [Fact]
    public async Task GetAsync_ShouldExcludeBotTraffic_WhenFilterIsHumanOnly()
    {
        await eventRepository.SaveAsync(PageView(EventClassification.Human));
        await eventRepository.SaveAsync(PageView(EventClassification.Bot));
        await eventRepository.SaveAsync(Click(EventClassification.Bot));

        var result = await releaseAnalytics.GetAsync(release.Id.ToString(), TrafficFilter.HumanOnly);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Views);
        Assert.Equal(0, result.Value.Clicks);
    }

    [Fact]
    public async Task GetAsync_ShouldBreakDownByCountry()
    {
        await eventRepository.SaveAsync(PageView(EventClassification.Human, "US"));
        await eventRepository.SaveAsync(PageView(EventClassification.Human, "US"));
        await eventRepository.SaveAsync(PageView(EventClassification.Human, "PT"));

        var result = await releaseAnalytics.GetAsync(release.Id.ToString(), TrafficFilter.All);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Countries, b => b.Label == "US" && b.Count == 2);
        Assert.Contains(result.Value.Countries, b => b.Label == "PT" && b.Count == 1);
    }
}
