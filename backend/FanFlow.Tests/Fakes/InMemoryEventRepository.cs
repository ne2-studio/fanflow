using FanFlow.Ports.Input;
using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class InMemoryEventRepository : IEventRepository
{
    private readonly Dictionary<Guid, TrackedEvent> storage = new();

    public Task SaveAsync(TrackedEvent trackedEvent)
    {
        storage[trackedEvent.Id] = trackedEvent;
        return Task.CompletedTask;
    }

    public Task<TrackedEvent?> LoadByIdAsync(Guid id)
    {
        return Task.FromResult(storage.GetValueOrDefault(id));
    }

    public Task<IEnumerable<TrackedEvent>> ListUnclassifiedAsync(EventType type, int batchSize)
    {
        var events = storage.Values
            .Where(e => e.Type == type && e.BotScore == null)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize);

        return Task.FromResult(events);
    }

    public Task<int> CountRecentByIpAsync(string ipAddress, DateTime since)
    {
        var count = storage.Values.Count(e => e.IpAddress == ipAddress && e.CreatedAt >= since);
        return Task.FromResult(count);
    }

    public Task<EventAnalyticsSummary> GetAnalyticsSummaryAsync(Guid releaseId, TrafficFilter filter)
    {
        var events = storage.Values.Where(e => e.ReleaseId == releaseId).ToList();
        var views = events.Where(e => e.Type == EventType.PageView).ToList();
        var clicks = events.Where(e => e.Type == EventType.DestinationClick).ToList();

        bool Included(TrackedEvent e) => filter == TrafficFilter.All || e.Classification == EventClassification.Human;

        var filteredViews = views.Where(Included).ToList();
        var filteredClicks = clicks.Where(Included).ToList();

        var summary = new EventAnalyticsSummary(
            filteredViews.Count,
            views.Count(e => e.Classification == EventClassification.Human),
            filteredClicks.Count,
            Breakdown(filteredViews, e => string.IsNullOrWhiteSpace(e.Referrer) ? "Direct" : e.Referrer!),
            Breakdown(filteredViews, e => e.Country ?? "Unknown"),
            Breakdown(filteredViews, e => e.UserAgent));

        return Task.FromResult(summary);
    }

    private static IReadOnlyList<EventCountBreakdown> Breakdown(IEnumerable<TrackedEvent> events, Func<TrackedEvent, string> keySelector) =>
        events.GroupBy(keySelector).Select(g => new EventCountBreakdown(g.Key, g.Count())).ToList();
}
