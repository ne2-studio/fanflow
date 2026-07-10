namespace FanFlow.Ports.Output;

public record EventAnalyticsSummary
(
    int Views,
    int QualifiedViews,
    int Clicks,
    IReadOnlyList<EventCountBreakdown> TrafficSources,
    IReadOnlyList<EventCountBreakdown> Countries,
    IReadOnlyList<EventCountBreakdown> Devices
);
