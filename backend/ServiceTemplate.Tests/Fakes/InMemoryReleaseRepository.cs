using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Tests.Fakes;

public class InMemoryReleaseRepository : IReleaseRepository
{
    private readonly Dictionary<Guid, Release> storage = new();

    public Task SaveAsync(Release release)
    {
        storage[release.Id] = release;
        return Task.CompletedTask;
    }

    public Task<Release?> LoadByIdAsync(Guid id, string tenantId)
    {
        var release = storage.GetValueOrDefault(id);
        return Task.FromResult(release != null && release.TenantId == tenantId ? release : null);
    }

    public Task<Release?> LoadBySlugAsync(string slug)
    {
        return Task.FromResult(storage.Values.FirstOrDefault(r => r.Slug == slug));
    }

    public Task<IEnumerable<Release>> ListAsync(string tenantId)
    {
        return Task.FromResult(storage.Values.Where(r => r.TenantId == tenantId));
    }

    public Task DeleteAsync(Guid id, string tenantId)
    {
        if (storage.TryGetValue(id, out var release) && release.TenantId == tenantId)
        {
            storage[id] = release with { Status = ReleaseStatus.Deleted };
        }

        return Task.CompletedTask;
    }
}
