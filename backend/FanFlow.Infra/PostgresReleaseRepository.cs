using System.Text.Json;
using Dapper;
using Npgsql;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public class PostgresReleaseRepository(string connectionString) : IReleaseRepository
{
    public async Task SaveAsync(Release release)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"
            INSERT INTO ""Releases""
                (""Id"", ""TenantId"", ""Slug"", ""Title"", ""Headline"", ""Description"", ""CoverImageUrl"",
                 ""BackgroundImageUrl"", ""CtaText"", ""FacebookPixelId"", ""LinksJson"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@Id, @TenantId, @Slug, @Title, @Headline, @Description, @CoverImageUrl,
                 @BackgroundImageUrl, @CtaText, @FacebookPixelId, @LinksJson, @Status, @CreatedAt, @UpdatedAt)
            ON CONFLICT (""Id"") DO UPDATE SET
                ""Slug"" = EXCLUDED.""Slug"",
                ""Title"" = EXCLUDED.""Title"",
                ""Headline"" = EXCLUDED.""Headline"",
                ""Description"" = EXCLUDED.""Description"",
                ""CoverImageUrl"" = EXCLUDED.""CoverImageUrl"",
                ""BackgroundImageUrl"" = EXCLUDED.""BackgroundImageUrl"",
                ""CtaText"" = EXCLUDED.""CtaText"",
                ""FacebookPixelId"" = EXCLUDED.""FacebookPixelId"",
                ""LinksJson"" = EXCLUDED.""LinksJson"",
                ""Status"" = EXCLUDED.""Status"",
                ""UpdatedAt"" = EXCLUDED.""UpdatedAt""";

        await connection.ExecuteAsync(sql, ToRow(release));
    }

    public async Task<Release?> LoadByIdAsync(Guid id, string tenantId)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"SELECT * FROM ""Releases"" WHERE ""Id"" = @Id AND ""TenantId"" = @TenantId";

        var row = await connection.QuerySingleOrDefaultAsync<ReleaseRow>(sql, new { Id = id, TenantId = tenantId });
        return row == null ? null : ToRelease(row);
    }

    public async Task<Release?> LoadByIdAsync(Guid id)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"SELECT * FROM ""Releases"" WHERE ""Id"" = @Id";

        var row = await connection.QuerySingleOrDefaultAsync<ReleaseRow>(sql, new { Id = id });
        return row == null ? null : ToRelease(row);
    }

    public async Task<Release?> LoadBySlugAsync(string slug)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"SELECT * FROM ""Releases"" WHERE ""Slug"" = @Slug";

        var row = await connection.QuerySingleOrDefaultAsync<ReleaseRow>(sql, new { Slug = slug });
        return row == null ? null : ToRelease(row);
    }

    public async Task<IEnumerable<Release>> ListAsync(string tenantId)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"SELECT * FROM ""Releases"" WHERE ""TenantId"" = @TenantId ORDER BY ""CreatedAt"" DESC";

        var rows = await connection.QueryAsync<ReleaseRow>(sql, new { TenantId = tenantId });
        return rows.Select(ToRelease);
    }

    public async Task DeleteAsync(Guid id, string tenantId)
    {
        using var connection = new NpgsqlConnection(connectionString);
        var sql = @"UPDATE ""Releases"" SET ""Status"" = @Status, ""UpdatedAt"" = @UpdatedAt
                    WHERE ""Id"" = @Id AND ""TenantId"" = @TenantId";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = tenantId,
            Status = ReleaseStatus.Deleted.ToString(),
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static object ToRow(Release release) => new
    {
        release.Id,
        release.TenantId,
        release.Slug,
        release.Title,
        release.Headline,
        release.Description,
        release.CoverImageUrl,
        release.BackgroundImageUrl,
        release.CtaText,
        release.FacebookPixelId,
        LinksJson = JsonSerializer.Serialize(release.Links),
        Status = release.Status.ToString(),
        release.CreatedAt,
        release.UpdatedAt
    };

    private static Release ToRelease(ReleaseRow row) => new(
        row.Id,
        row.TenantId,
        row.Slug,
        row.Title,
        row.Headline,
        row.Description,
        row.CoverImageUrl,
        row.BackgroundImageUrl,
        row.CtaText,
        row.FacebookPixelId,
        JsonSerializer.Deserialize<List<DestinationLink>>(row.LinksJson) ?? [],
        Enum.Parse<ReleaseStatus>(row.Status),
        row.CreatedAt,
        row.UpdatedAt);

    private class ReleaseRow
    {
        public Guid Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Headline { get; set; } = "";
        public string Description { get; set; } = "";
        public string CoverImageUrl { get; set; } = "";
        public string BackgroundImageUrl { get; set; } = "";
        public string CtaText { get; set; } = "";
        public string FacebookPixelId { get; set; } = "";
        public string LinksJson { get; set; } = "[]";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
