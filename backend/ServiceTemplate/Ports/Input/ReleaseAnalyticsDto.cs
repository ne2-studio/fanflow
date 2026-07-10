namespace ServiceTemplate.Ports.Input;

public record ReleaseAnalyticsDto
(
    int Views,
    int QualifiedViews,
    int Clicks,
    double Ctr,
    IReadOnlyList<BreakdownItemDto> TrafficSources,
    IReadOnlyList<BreakdownItemDto> Countries,
    IReadOnlyList<BreakdownItemDto> Devices
);
