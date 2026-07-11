using CSharpFunctionalExtensions;

namespace FanFlow.Ports.Input;

/// <summary>
/// Read-only reporting surface for a release's traffic. "Qualified" figures exclude
/// Bot-classified events; the traffic filter toggles between all traffic and human-only.
/// </summary>
public interface IReleaseAnalytics
{
    /// <returns>The analytics summary, or a failure: "release_not_found" (idempotent).</returns>
    Task<Result<ReleaseAnalyticsDto>> GetAsync(string releaseId, TrafficFilter filter);

    /// <summary>
    /// Raw, unaggregated list of every tracked event (PageView, DestinationClick, HoneypotHit)
    /// recorded for the release, newest first — for audit/investigation purposes.
    /// </summary>
    /// <returns>The event list (possibly empty), or a failure: "release_not_found" (idempotent).</returns>
    Task<Result<IReadOnlyList<TrackedEventDto>>> ListEventsAsync(string releaseId);
}
