using Microsoft.Extensions.Logging.Abstractions;
using FanFlow.Application;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;
using FanFlow.Tests.Fakes;

namespace FanFlow.Tests;

public class TrafficTrackerTests
{
    private static readonly Guid GeneratedId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InMemoryReleaseRepository releaseRepository;
    private readonly InMemoryEventRepository eventRepository;
    private readonly TrafficTracker trafficTracker;
    private readonly Release release;

    public TrafficTrackerTests()
    {
        releaseRepository = new InMemoryReleaseRepository();
        eventRepository = new InMemoryEventRepository();
        trafficTracker = new TrafficTracker(
            NullLogger<TrafficTracker>.Instance, releaseRepository, eventRepository, new StaticIdGenerator(GeneratedId), new StaticClock());

        release = new Release(
            Guid.NewGuid(), "user-1", "run-to-me", "The Artist", "Run To Me", "headline", "desc", "cover", "Listen", "123456789012345",
            [new DestinationLink("Spotify", "https://open.spotify.com/track/123")], ReleaseStatus.Published, DateTime.UtcNow, DateTime.UtcNow);
        releaseRepository.SaveAsync(release).Wait();
    }

    [Fact]
    public async Task TrackPageViewAsync_ShouldRecordUnclassifiedEvent()
    {
        var result = await trafficTracker.TrackPageViewAsync(new TrackPageViewRequest("run-to-me", "1.2.3.4", "Mozilla/5.0", "https://instagram.com", "US"));

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(GeneratedId);
        Assert.NotNull(stored);
        Assert.Equal(EventType.PageView, stored!.Type);
        Assert.Null(stored.Classification);
    }

    [Fact]
    public async Task TrackPageViewAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await trafficTracker.TrackPageViewAsync(new TrackPageViewRequest("missing", "1.2.3.4", "Mozilla/5.0", null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task TrackDestinationClickAsync_ShouldRecordEventAndReturnRedirectUrl()
    {
        var result = await trafficTracker.TrackDestinationClickAsync(new TrackDestinationClickRequest(
            "run-to-me", "spotify", "1.2.3.4", "Mozilla/5.0", "https://instagram.com", 3200, "US"));

        Assert.True(result.IsSuccess);
        Assert.Equal("https://open.spotify.com/track/123", result.Value.Url);

        var stored = await eventRepository.LoadByIdAsync(GeneratedId);
        Assert.Equal(EventType.DestinationClick, stored!.Type);
        Assert.Equal(3200, stored.DwellTimeMs);
    }

    [Fact]
    public async Task TrackDestinationClickAsync_ShouldFail_WhenDestinationDoesNotExist()
    {
        var result = await trafficTracker.TrackDestinationClickAsync(new TrackDestinationClickRequest(
            "run-to-me", "apple-music", "1.2.3.4", "Mozilla/5.0", null, 1000, null));

        Assert.True(result.IsFailure);
        Assert.Equal("destination_not_found", result.Error);
    }

    [Fact]
    public async Task TrackDestinationClickAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await trafficTracker.TrackDestinationClickAsync(new TrackDestinationClickRequest(
            "missing", "spotify", "1.2.3.4", "Mozilla/5.0", null, 1000, null));

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task RecordHoneypotHitAsync_ShouldClassifyBotImmediately()
    {
        var result = await trafficTracker.RecordHoneypotHitAsync(new RecordHoneypotHitRequest("run-to-me", "1.2.3.4", "curl/8.0"));

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(GeneratedId);
        Assert.Equal(EventType.HoneypotHit, stored!.Type);
        Assert.Equal(EventClassification.Bot, stored.Classification);
        Assert.Equal(100, stored.BotScore);
    }

    [Fact]
    public async Task RecordHoneypotHitAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await trafficTracker.RecordHoneypotHitAsync(new RecordHoneypotHitRequest("missing", "1.2.3.4", "curl/8.0"));

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }
}
