namespace ServiceTemplate.Ports.Output;

public interface IReleaseRepository
{
    Task SaveAsync(Release release);
    Task<Release?> LoadByIdAsync(Guid id, string tenantId);

    /// <summary>
    /// Looks up a release by its public slug, regardless of tenant — used to resolve
    /// the release for unauthenticated traffic-tracking requests (/pv/*, /out/*, /trap/*).
    /// </summary>
    Task<Release?> LoadBySlugAsync(string slug);
    Task<IEnumerable<Release>> ListAsync(string tenantId);

    /// <summary>
    /// Soft-deletes the release (marks it Deleted / unpublished) while retaining its
    /// historical tracking data.
    /// </summary>
    Task DeleteAsync(Guid id, string tenantId);
}
