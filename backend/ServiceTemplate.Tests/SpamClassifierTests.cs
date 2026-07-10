using Microsoft.Extensions.Logging.Abstractions;
using ServiceTemplate.Application;
using ServiceTemplate.Ports.Output;
using ServiceTemplate.Tests.Fakes;

namespace ServiceTemplate.Tests;

public class SpamClassifierTests
{
    private readonly InMemoryEventRepository eventRepository;
    private readonly SpyConversionsApiClient conversionsApiClient;
    private readonly StaticClock clock;
    private readonly SpamClassifier spamClassifier;
    private readonly Guid releaseId = Guid.NewGuid();

    public SpamClassifierTests()
    {
        eventRepository = new InMemoryEventRepository();
        conversionsApiClient = new SpyConversionsApiClient();
        clock = new StaticClock();
        spamClassifier = new SpamClassifier(NullLogger<SpamClassifier>.Instance, eventRepository, conversionsApiClient, clock);
    }

    private TrackedEvent PageView(string userAgent) => new(
        Guid.NewGuid(), releaseId, EventType.PageView, "1.2.3.4", userAgent, null, null, null, "US", null, null, clock.UtcNow());

    [Fact]
    public async Task AnalyzePageViewAsync_ShouldClassifyBot_WhenUserAgentIsKnownCrawler()
    {
        var pageView = PageView("Googlebot/2.1 (+http://www.google.com/bot.html)");
        await eventRepository.SaveAsync(pageView);

        var result = await spamClassifier.AnalyzePageViewAsync(pageView.Id.ToString());

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(pageView.Id);
        Assert.Equal(EventClassification.Bot, stored!.Classification);
        Assert.Empty(conversionsApiClient.SentEvents);
    }

    [Fact]
    public async Task AnalyzePageViewAsync_ShouldClassifyHumanAndForwardConversion_WhenSignalsAreClean()
    {
        var pageView = PageView("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15");
        await eventRepository.SaveAsync(pageView);

        var result = await spamClassifier.AnalyzePageViewAsync(pageView.Id.ToString());

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(pageView.Id);
        Assert.Equal(EventClassification.Human, stored!.Classification);
        Assert.Single(conversionsApiClient.SentEvents);
        Assert.Equal(releaseId, conversionsApiClient.SentEvents[0].ReleaseId);
        Assert.Equal(EventType.PageView, conversionsApiClient.SentEvents[0].Type);
    }

    [Fact]
    public async Task AnalyzePageViewAsync_ShouldBeNoOp_WhenEventAlreadyClassified()
    {
        var pageView = PageView("Googlebot/2.1") with { BotScore = 10, Classification = EventClassification.Human };
        await eventRepository.SaveAsync(pageView);

        var result = await spamClassifier.AnalyzePageViewAsync(pageView.Id.ToString());

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(pageView.Id);
        Assert.Equal(10, stored!.BotScore);
        Assert.Empty(conversionsApiClient.SentEvents);
    }

    [Fact]
    public async Task AnalyzePageViewAsync_ShouldBeNoOp_WhenEventDoesNotExist()
    {
        var result = await spamClassifier.AnalyzePageViewAsync(Guid.NewGuid().ToString());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AnalyzeDestinationClickAsync_ShouldClassifyBot_WhenFrequencyAndDwellTimeCombine()
    {
        const string ip = "9.9.9.9";
        for (var i = 0; i < 25; i++)
        {
            await eventRepository.SaveAsync(new TrackedEvent(
                Guid.NewGuid(), releaseId, EventType.PageView, ip, "Mozilla/5.0", null, null, null, "US", null, null, clock.UtcNow()));
        }

        var click = new TrackedEvent(
            Guid.NewGuid(), releaseId, EventType.DestinationClick, ip, "Mozilla/5.0", null, "spotify", 100, "US", null, null, clock.UtcNow());
        await eventRepository.SaveAsync(click);

        var result = await spamClassifier.AnalyzeDestinationClickAsync(click.Id.ToString());

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(click.Id);
        Assert.Equal(EventClassification.Bot, stored!.Classification);
        Assert.Empty(conversionsApiClient.SentEvents);
    }

    [Fact]
    public async Task AnalyzeDestinationClickAsync_ShouldClassifyHuman_WhenDwellTimeIsNormal()
    {
        var click = new TrackedEvent(
            Guid.NewGuid(), releaseId, EventType.DestinationClick, "1.2.3.4", "Mozilla/5.0", null, "spotify", 5000, "US", null, null, clock.UtcNow());
        await eventRepository.SaveAsync(click);

        var result = await spamClassifier.AnalyzeDestinationClickAsync(click.Id.ToString());

        Assert.True(result.IsSuccess);
        var stored = await eventRepository.LoadByIdAsync(click.Id);
        Assert.Equal(EventClassification.Human, stored!.Classification);
        Assert.Single(conversionsApiClient.SentEvents);
    }
}
