using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;

namespace FanFlow.Application;

public class ReleaseAnalytics(
    ILogger<ReleaseAnalytics> logger,
    IReleaseRepository releaseRepository,
    IEventRepository eventRepository,
    ICurrentUserProvider currentUserProvider) : IReleaseAnalytics
{
    public async Task<Result<ReleaseAnalyticsDto>> GetAsync(string releaseId, TrafficFilter filter)
    {
        logger.LogInformation("GetAsync - Fetching analytics for release {Id} with filter {Filter}", releaseId, filter);

        if (!Guid.TryParse(releaseId, out var id))
            return Result.Failure<ReleaseAnalyticsDto>("release_not_found");

        var tenantId = currentUserProvider.GetUserId();
        var release = await releaseRepository.LoadByIdAsync(id, tenantId);

        if (release == null || release.Status == ReleaseStatus.Deleted)
            return Result.Failure<ReleaseAnalyticsDto>("release_not_found");

        var summary = await eventRepository.GetAnalyticsSummaryAsync(release.Id, filter);

        var ctr = summary.Views > 0 ? (double)summary.Clicks / summary.Views : 0d;

        var dto = new ReleaseAnalyticsDto(
            summary.Views,
            summary.QualifiedViews,
            summary.Clicks,
            ctr,
            ToBreakdown(summary.TrafficSources),
            ToBreakdown(summary.Countries),
            ToBreakdown(summary.Devices));

        return Result.Success(dto);
    }

    private static IReadOnlyList<BreakdownItemDto> ToBreakdown(IReadOnlyList<EventCountBreakdown> breakdown) =>
        breakdown.Select(b => new BreakdownItemDto(b.Label, b.Count)).ToList();
}
