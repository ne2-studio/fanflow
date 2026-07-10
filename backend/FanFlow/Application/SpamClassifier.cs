using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;

namespace FanFlow.Application;

public class SpamClassifier(
    ILogger<SpamClassifier> logger,
    IEventRepository eventRepository,
    IReleaseRepository releaseRepository,
    IConversionsApiClient conversionsApiClient,
    IClock clock) : ISpamClassifier
{
    public Task<Result> AnalyzePageViewAsync(string pageViewId) => AnalyzeAsync(pageViewId, EventType.PageView);

    public Task<Result> AnalyzeDestinationClickAsync(string clickId) => AnalyzeAsync(clickId, EventType.DestinationClick);

    private async Task<Result> AnalyzeAsync(string eventId, EventType expectedType)
    {
        logger.LogInformation("AnalyzeAsync - Analyzing {Type} event {Id}", expectedType, eventId);

        // Missing/already-classified/wrong-type events are treated as a no-op rather than a
        // failure: this method is driven by a background poll over unclassified events
        // (see IEventRepository.ListUnclassifiedAsync), not by a user-facing request, and
        // CONTRACT.md leaves failure/timeout semantics as TODO — a quiet no-op is the safest
        // idempotent default until that's confirmed with business.
        if (!Guid.TryParse(eventId, out var id))
        {
            logger.LogWarning("AnalyzeAsync - {Id} is not a valid event id", eventId);
            return Result.Success();
        }

        var trackedEvent = await eventRepository.LoadByIdAsync(id);
        if (trackedEvent == null || trackedEvent.Type != expectedType || trackedEvent.Classification != null)
        {
            logger.LogWarning("AnalyzeAsync - Event {Id} is not an unclassified {Type} event", eventId, expectedType);
            return Result.Success();
        }

        var since = clock.UtcNow() - BotScoring.RequestFrequencyWindow;
        var recentRequests = await eventRepository.CountRecentByIpAsync(trackedEvent.IpAddress, since);
        var (score, classification) = BotScoring.Score(trackedEvent, recentRequests);

        var classified = trackedEvent with { BotScore = score, Classification = classification };
        await eventRepository.SaveAsync(classified);

        logger.LogInformation("AnalyzeAsync - Event {Id} classified as {Classification} (score {Score})", eventId, classification, score);

        if (classification == EventClassification.Human)
            await ForwardConversionAsync(classified);

        return Result.Success();
    }

    private async Task ForwardConversionAsync(TrackedEvent trackedEvent)
    {
        var release = await releaseRepository.LoadByIdAsync(trackedEvent.ReleaseId);
        if (release == null)
        {
            logger.LogWarning("ForwardConversionAsync - Release {ReleaseId} not found, skipping forward for event {Id}", trackedEvent.ReleaseId, trackedEvent.Id);
            return;
        }

        var conversionEvent = new ConversionEvent(
            trackedEvent.Type,
            trackedEvent.ReleaseId,
            release.FacebookPixelId,
            trackedEvent.IpAddress,
            trackedEvent.UserAgent,
            trackedEvent.CreatedAt);

        var result = await conversionsApiClient.SendConversionEventAsync(conversionEvent);
        if (result.IsFailure)
        {
            // Forwarding failure never fails the classification itself — the event is already
            // classified and won't be re-picked-up by ListUnclassifiedAsync. Retry/dedupe policy
            // for Meta forwarding is TODO: confirm with business (see CONTRACT.md #11).
            logger.LogError("ForwardConversionAsync - Failed to forward event {Id} to Meta: {Error}", trackedEvent.Id, result.Error);
        }
    }
}
