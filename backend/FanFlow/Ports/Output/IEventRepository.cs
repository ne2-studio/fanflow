using FanFlow.Ports.Input;

namespace FanFlow.Ports.Output;

public interface IEventRepository
{
    Task SaveAsync(TrackedEvent trackedEvent);
    Task<TrackedEvent?> LoadByIdAsync(Guid id);

    /// <summary>
    /// Events of the given type that have not been through spam analysis yet (BotScore/Classification
    /// still null), oldest first — polled by the async classification worker.
    /// </summary>
    Task<IEnumerable<TrackedEvent>> ListUnclassifiedAsync(EventType type, int batchSize);

    /// <summary>
    /// Number of events recorded from the given IP address since the given point in time — feeds the
    /// request-frequency/rate-limit bot signal.
    /// </summary>
    Task<int> CountRecentByIpAsync(string ipAddress, DateTime since);

    Task<EventAnalyticsSummary> GetAnalyticsSummaryAsync(Guid releaseId, TrafficFilter filter);
}
