using CSharpFunctionalExtensions;

namespace ServiceTemplate.Ports.Input;

/// <summary>
/// Read-only reporting surface for a release's traffic. "Qualified" figures exclude
/// Bot-classified events; the traffic filter toggles between all traffic and human-only.
/// </summary>
public interface IReleaseAnalytics
{
    /// <returns>The analytics summary, or a failure: "release_not_found" (idempotent).</returns>
    Task<Result<ReleaseAnalyticsDto>> GetAsync(string releaseId, TrafficFilter filter);
}
