using Dapper;
using Npgsql;
using ServiceTemplate.Ports.Input;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Infra;

public class PostgresEventRepository(string connectionString) : IEventRepository
{
    public async Task SaveAsync(TrackedEvent trackedEvent)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"
            INSERT INTO ""Events""
                (""Id"", ""ReleaseId"", ""Type"", ""IpAddress"", ""UserAgent"", ""Referrer"", ""DestinationId"",
                 ""DwellTimeMs"", ""Country"", ""BotScore"", ""Classification"", ""CreatedAt"")
            VALUES
                (@Id, @ReleaseId, @Type, @IpAddress, @UserAgent, @Referrer, @DestinationId,
                 @DwellTimeMs, @Country, @BotScore, @Classification, @CreatedAt)
            ON CONFLICT (""Id"") DO UPDATE SET
                ""BotScore"" = EXCLUDED.""BotScore"",
                ""Classification"" = EXCLUDED.""Classification""";

        await connection.ExecuteAsync(sql, ToRow(trackedEvent));
    }

    public async Task<TrackedEvent?> LoadByIdAsync(Guid id)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<EventRow>(
            @"SELECT * FROM ""Events"" WHERE ""Id"" = @Id", new { Id = id });

        return row == null ? null : ToTrackedEvent(row);
    }

    public async Task<IEnumerable<TrackedEvent>> ListUnclassifiedAsync(EventType type, int batchSize)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"
            SELECT * FROM ""Events""
            WHERE ""Type"" = @Type AND ""BotScore"" IS NULL
            ORDER BY ""CreatedAt""
            LIMIT @BatchSize";

        var rows = await connection.QueryAsync<EventRow>(sql, new { Type = type.ToString(), BatchSize = batchSize });
        return rows.Select(ToTrackedEvent);
    }

    public async Task<int> CountRecentByIpAsync(string ipAddress, DateTime since)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"
            SELECT COUNT(*) FROM ""Events""
            WHERE ""IpAddress"" = @IpAddress AND ""CreatedAt"" >= @Since";

        return await connection.ExecuteScalarAsync<int>(sql, new { IpAddress = ipAddress, Since = since });
    }

    public async Task<EventAnalyticsSummary> GetAnalyticsSummaryAsync(Guid releaseId, TrafficFilter filter)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"
            SELECT * FROM ""Events""
            WHERE ""ReleaseId"" = @ReleaseId AND ""Type"" IN ('PageView', 'DestinationClick')";

        var rows = await connection.QueryAsync<EventRow>(sql, new { ReleaseId = releaseId });
        var events = rows.Select(ToTrackedEvent).ToList();

        var views = events.Where(e => e.Type == EventType.PageView).ToList();
        var clicks = events.Where(e => e.Type == EventType.DestinationClick).ToList();

        // "Qualified" always reflects the Human-classified subset regardless of the applied filter
        // (CONTRACT.md: "'qualified' figures exclude Bot-classified events"); the filter itself
        // controls what Views/Clicks/breakdowns count — All traffic vs. Human-only.
        bool Included(TrackedEvent e) => filter == TrafficFilter.All || e.Classification == EventClassification.Human;

        var filteredViews = views.Where(Included).ToList();
        var filteredClicks = clicks.Where(Included).ToList();

        return new EventAnalyticsSummary(
            filteredViews.Count,
            views.Count(e => e.Classification == EventClassification.Human),
            filteredClicks.Count,
            Breakdown(filteredViews, TrafficSource),
            Breakdown(filteredViews, e => string.IsNullOrWhiteSpace(e.Country) ? "Unknown" : e.Country!),
            Breakdown(filteredViews, e => DeviceFromUserAgent(e.UserAgent)));
    }

    private static IReadOnlyList<EventCountBreakdown> Breakdown(IEnumerable<TrackedEvent> events, Func<TrackedEvent, string> keySelector) =>
        events
            .GroupBy(keySelector)
            .Select(g => new EventCountBreakdown(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

    private static string TrafficSource(TrackedEvent e)
    {
        if (string.IsNullOrWhiteSpace(e.Referrer))
            return "Direct";

        return Uri.TryCreate(e.Referrer, UriKind.Absolute, out var uri) ? uri.Host : "Direct";
    }

    private static string DeviceFromUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Unknown";

        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
            return "Tablet";

        if (userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return "Mobile";

        return "Desktop";
    }

    private static object ToRow(TrackedEvent e) => new
    {
        e.Id,
        e.ReleaseId,
        Type = e.Type.ToString(),
        e.IpAddress,
        e.UserAgent,
        e.Referrer,
        e.DestinationId,
        e.DwellTimeMs,
        e.Country,
        e.BotScore,
        Classification = e.Classification?.ToString(),
        e.CreatedAt
    };

    private static TrackedEvent ToTrackedEvent(EventRow row) => new(
        row.Id,
        row.ReleaseId,
        Enum.Parse<EventType>(row.Type),
        row.IpAddress,
        row.UserAgent,
        row.Referrer,
        row.DestinationId,
        row.DwellTimeMs,
        row.Country,
        row.BotScore,
        row.Classification == null ? null : Enum.Parse<EventClassification>(row.Classification),
        row.CreatedAt);

    private class EventRow
    {
        public Guid Id { get; set; }
        public Guid ReleaseId { get; set; }
        public string Type { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public string? Referrer { get; set; }
        public string? DestinationId { get; set; }
        public int? DwellTimeMs { get; set; }
        public string? Country { get; set; }
        public int? BotScore { get; set; }
        public string? Classification { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
