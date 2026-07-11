using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;

namespace FanFlow.Application;

public class TrafficTracker(
    ILogger<TrafficTracker> logger,
    IReleaseRepository releaseRepository,
    IEventRepository eventRepository,
    IIdGenerator idGenerator,
    IClock clock) : ITrafficTracker
{
    public async Task<Result> TrackPageViewAsync(TrackPageViewRequest request)
    {
        var release = await releaseRepository.LoadBySlugAsync(request.ReleaseSlug);
        if (release == null || release.Status == ReleaseStatus.Deleted)
        {
            logger.LogWarning("TrackPageViewAsync - Release {Slug} does not exist", request.ReleaseSlug);
            return Result.Failure("release_not_found");
        }

        var trackedEvent = new TrackedEvent(
            idGenerator.NewId(),
            release.Id,
            EventType.PageView,
            request.IpAddress,
            request.UserAgent,
            request.Referrer,
            DestinationId: null,
            DwellTimeMs: null,
            request.Country,
            BotScore: null,
            Classification: null,
            clock.UtcNow());

        await eventRepository.SaveAsync(trackedEvent);

        logger.LogInformation("TrackPageViewAsync - Recorded PageView {Id} for release {ReleaseId}", trackedEvent.Id, release.Id);
        return Result.Success();
    }

    public async Task<Result> TrackDestinationClickAsync(TrackDestinationClickRequest request)
    {
        var release = await releaseRepository.LoadBySlugAsync(request.ReleaseSlug);
        if (release == null || release.Status == ReleaseStatus.Deleted)
        {
            logger.LogWarning("TrackDestinationClickAsync - Release {Slug} does not exist", request.ReleaseSlug);
            return Result.Failure("release_not_found");
        }

        var destination = release.Links.FirstOrDefault(l =>
            string.Equals(l.Platform, request.DestinationId, StringComparison.OrdinalIgnoreCase));

        if (destination == null)
        {
            logger.LogWarning("TrackDestinationClickAsync - Destination {DestinationId} not found on release {ReleaseId}", request.DestinationId, release.Id);
            return Result.Failure("destination_not_found");
        }

        var trackedEvent = new TrackedEvent(
            idGenerator.NewId(),
            release.Id,
            EventType.DestinationClick,
            request.IpAddress,
            request.UserAgent,
            request.Referrer,
            request.DestinationId,
            request.DwellTimeMs,
            request.Country,
            BotScore: null,
            Classification: null,
            clock.UtcNow());

        await eventRepository.SaveAsync(trackedEvent);

        logger.LogInformation("TrackDestinationClickAsync - Recorded DestinationClick {Id} for release {ReleaseId}", trackedEvent.Id, release.Id);
        return Result.Success();
    }

    public async Task<Result> RecordHoneypotHitAsync(RecordHoneypotHitRequest request)
    {
        var release = await releaseRepository.LoadBySlugAsync(request.ReleaseSlug);
        if (release == null || release.Status == ReleaseStatus.Deleted)
        {
            logger.LogWarning("RecordHoneypotHitAsync - Release {Slug} does not exist", request.ReleaseSlug);
            return Result.Failure("release_not_found");
        }

        // Honeypot hits are a definitive, synchronous Bot signal — no async classification needed.
        var trackedEvent = new TrackedEvent(
            idGenerator.NewId(),
            release.Id,
            EventType.HoneypotHit,
            request.IpAddress,
            request.UserAgent,
            Referrer: null,
            DestinationId: null,
            DwellTimeMs: null,
            Country: null,
            BotScore: 100,
            Classification: EventClassification.Bot,
            clock.UtcNow());

        await eventRepository.SaveAsync(trackedEvent);

        logger.LogInformation("RecordHoneypotHitAsync - Recorded HoneypotHit {Id} for release {ReleaseId}", trackedEvent.Id, release.Id);
        return Result.Success();
    }
}
