using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using ServiceTemplate.Ports.Input;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Application;

public class ReleaseManager(
    ILogger<ReleaseManager> logger,
    IReleaseRepository repository,
    IReleasePublisher publisher,
    ISlugGenerator slugGenerator,
    IIdGenerator idGenerator,
    IClock clock,
    ICurrentUserProvider currentUserProvider) : IReleaseManager
{
    private const string SpotifyPlatform = "spotify";

    public async Task<Result<ReleaseDto>> CreateAsync(CreateReleaseRequest request)
    {
        logger.LogInformation("CreateAsync - Creating release {Title}", request.Title);

        var invalidDestination = ValidateDestinations(request.Links);
        if (invalidDestination != null)
            return Result.Failure<ReleaseDto>(invalidDestination);

        var tenantId = currentUserProvider.GetUserId();
        var slug = slugGenerator.Generate(request.Title);

        if (await repository.LoadBySlugAsync(slug) != null)
        {
            logger.LogWarning("CreateAsync - Slug {Slug} is already taken", slug);
            return Result.Failure<ReleaseDto>("slug_taken");
        }

        var now = clock.UtcNow();
        var release = new Release(
            idGenerator.NewId(),
            tenantId,
            slug,
            request.Title,
            request.Headline,
            request.Description,
            request.CoverImageUrl,
            request.BackgroundImageUrl,
            request.CtaText,
            ToLinks(request.Links),
            ReleaseStatus.Published,
            now,
            now);

        await repository.SaveAsync(release);
        await publisher.PublishAsync(release);

        logger.LogInformation("CreateAsync - Release {Id} created with slug {Slug}", release.Id, release.Slug);
        return ToDto(release);
    }

    public async Task<Result<ReleaseDto>> UpdateAsync(string releaseId, UpdateReleaseRequest request)
    {
        logger.LogInformation("UpdateAsync - Updating release {Id}", releaseId);

        if (!Guid.TryParse(releaseId, out var id))
            return Result.Failure<ReleaseDto>("release_not_found");

        var tenantId = currentUserProvider.GetUserId();
        var existing = await repository.LoadByIdAsync(id, tenantId);
        if (existing == null)
        {
            logger.LogWarning("UpdateAsync - Release {Id} does not exist", releaseId);
            return Result.Failure<ReleaseDto>("release_not_found");
        }

        if (request.Links != null)
        {
            var invalidDestination = ValidateDestinations(request.Links);
            if (invalidDestination != null)
                return Result.Failure<ReleaseDto>(invalidDestination);
        }

        var title = request.Title ?? existing.Title;

        // Slug is derived from the title, so a title change re-derives the slug. Not explicitly
        // spelled out for UpdateRelease in CONTRACT.md (only CreateRelease lists "slug_taken"),
        // but implied by "slug/URL can change after publish" — reusing CreateRelease's collision
        // semantics here since the same generator/uniqueness rule applies.
        var slug = existing.Slug;
        if (request.Title != null && request.Title != existing.Title)
        {
            var candidateSlug = slugGenerator.Generate(request.Title);
            if (candidateSlug != existing.Slug)
            {
                var collision = await repository.LoadBySlugAsync(candidateSlug);
                if (collision != null && collision.Id != id)
                {
                    logger.LogWarning("UpdateAsync - Slug {Slug} is already taken", candidateSlug);
                    return Result.Failure<ReleaseDto>("slug_taken");
                }

                slug = candidateSlug;
            }
        }

        var updated = existing with
        {
            Slug = slug,
            Title = title,
            Headline = request.Headline ?? existing.Headline,
            Description = request.Description ?? existing.Description,
            CoverImageUrl = request.CoverImageUrl ?? existing.CoverImageUrl,
            BackgroundImageUrl = request.BackgroundImageUrl ?? existing.BackgroundImageUrl,
            CtaText = request.CtaText ?? existing.CtaText,
            Links = request.Links != null ? ToLinks(request.Links) : existing.Links,
            UpdatedAt = clock.UtcNow()
        };

        await repository.SaveAsync(updated);
        await publisher.PublishAsync(updated);

        logger.LogInformation("UpdateAsync - Release {Id} updated", updated.Id);
        return ToDto(updated);
    }

    public async Task<Result<IEnumerable<ReleaseSummaryDto>>> ListAsync()
    {
        var tenantId = currentUserProvider.GetUserId();
        logger.LogInformation("ListAsync - Fetching releases for tenant {TenantId}", tenantId);

        var releases = await repository.ListAsync(tenantId);
        var dtos = releases
            .Where(r => r.Status != ReleaseStatus.Deleted)
            .Select(ToSummaryDto);

        return Result.Success(dtos);
    }

    public async Task<Result<ReleaseDto>> GetAsync(string releaseId)
    {
        if (!Guid.TryParse(releaseId, out var id))
            return Result.Failure<ReleaseDto>("release_not_found");

        var tenantId = currentUserProvider.GetUserId();
        var release = await repository.LoadByIdAsync(id, tenantId);

        if (release == null || release.Status == ReleaseStatus.Deleted)
            return Result.Failure<ReleaseDto>("release_not_found");

        return ToDto(release);
    }

    public async Task<Result> DeleteAsync(string releaseId)
    {
        logger.LogInformation("DeleteAsync - Deleting release {Id}", releaseId);

        if (!Guid.TryParse(releaseId, out var id))
            return Result.Failure("release_not_found");

        var tenantId = currentUserProvider.GetUserId();
        var release = await repository.LoadByIdAsync(id, tenantId);

        if (release == null || release.Status == ReleaseStatus.Deleted)
        {
            logger.LogWarning("DeleteAsync - Release {Id} does not exist", releaseId);
            return Result.Failure("release_not_found");
        }

        await repository.DeleteAsync(id, tenantId);
        await publisher.UnpublishAsync(release.Slug);

        logger.LogInformation("DeleteAsync - Release {Id} deleted", id);
        return Result.Success();
    }

    private static string? ValidateDestinations(IReadOnlyList<DestinationLinkDto> links)
    {
        return links.Any(l => !string.Equals(l.Platform, SpotifyPlatform, StringComparison.OrdinalIgnoreCase))
            ? "invalid_destination"
            : null;
    }

    private static IReadOnlyList<DestinationLink> ToLinks(IReadOnlyList<DestinationLinkDto> links) =>
        links.Select(l => new DestinationLink(l.Platform, l.Url)).ToList();

    private static IReadOnlyList<DestinationLinkDto> ToLinkDtos(IReadOnlyList<DestinationLink> links) =>
        links.Select(l => new DestinationLinkDto(l.Platform, l.Url)).ToList();

    private static ReleaseDto ToDto(Release release) =>
        new(
            release.Id.ToString(),
            release.Slug,
            $"fanflow.app/{release.Slug}",
            release.Title,
            release.Headline,
            release.Description,
            release.CoverImageUrl,
            release.BackgroundImageUrl,
            release.CtaText,
            ToLinkDtos(release.Links),
            release.Status.ToString(),
            release.CreatedAt);

    private static ReleaseSummaryDto ToSummaryDto(Release release) =>
        new(release.Id.ToString(), release.Title, release.Slug, release.Status.ToString(), release.CreatedAt);
}
