using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using FanFlow.Ports.Input;
using FanFlow.Ports.Output;

namespace FanFlow.Application;

public class ReleaseManager(
    ILogger<ReleaseManager> logger,
    IReleaseRepository repository,
    IReleasePublisher publisher,
    ISlugGenerator slugGenerator,
    IIdGenerator idGenerator,
    IClock clock,
    ICurrentUserProvider currentUserProvider,
    IPublicSiteSettings publicSiteSettings,
    IImageProcessor imageProcessor,
    IImageStorage imageStorage) : IReleaseManager
{
    private const string SpotifyPlatform = "spotify";
    private const int CoverImageSize = 420;
    private const int MaxCoverImageBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedCoverImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<Result<ReleaseDto>> CreateAsync(CreateReleaseRequest request)
    {
        logger.LogInformation("CreateAsync - Creating release {Title}", request.Title);

        var invalidDestination = ValidateDestinations(request.Links);
        if (invalidDestination != null)
            return Result.Failure<ReleaseDto>(invalidDestination);

        var invalidPixelId = ValidateFacebookPixelId(request.FacebookPixelId);
        if (invalidPixelId != null)
            return Result.Failure<ReleaseDto>(invalidPixelId);

        var invalidCoverImage = ValidateCoverImage(request.CoverImage, required: true);
        if (invalidCoverImage != null)
            return Result.Failure<ReleaseDto>(invalidCoverImage);

        var tenantId = currentUserProvider.GetUserId();
        var slug = slugGenerator.Generate(request.Title);

        if (await repository.LoadBySlugAsync(slug) != null)
        {
            logger.LogWarning("CreateAsync - Slug {Slug} is already taken", slug);
            return Result.Failure<ReleaseDto>("slug_taken");
        }

        var id = idGenerator.NewId();
        var coverImageUrl = await StoreCoverImageAsync(id, request.CoverImage!);

        var now = clock.UtcNow();
        var release = new Release(
            id,
            tenantId,
            slug,
            request.ArtistName,
            request.Title,
            request.Headline,
            request.Description,
            coverImageUrl,
            request.CtaText,
            request.FacebookPixelId,
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

        if (request.FacebookPixelId != null)
        {
            var invalidPixelId = ValidateFacebookPixelId(request.FacebookPixelId);
            if (invalidPixelId != null)
                return Result.Failure<ReleaseDto>(invalidPixelId);
        }

        var invalidCoverImage = ValidateCoverImage(request.CoverImage, required: false);
        if (invalidCoverImage != null)
            return Result.Failure<ReleaseDto>(invalidCoverImage);

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

        var coverImageUrl = request.CoverImage != null
            ? await StoreCoverImageAsync(existing.Id, request.CoverImage)
            : existing.CoverImageUrl;

        var updated = existing with
        {
            Slug = slug,
            ArtistName = request.ArtistName ?? existing.ArtistName,
            Title = title,
            Headline = request.Headline ?? existing.Headline,
            Description = request.Description ?? existing.Description,
            CoverImageUrl = coverImageUrl,
            CtaText = request.CtaText ?? existing.CtaText,
            FacebookPixelId = request.FacebookPixelId ?? existing.FacebookPixelId,
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

    // FanFlow requires a Meta Pixel on every release — attribution via Conversions API is core
    // to the product, not an optional add-on. Digits-only also guards the raw URL interpolation
    // in MetaConversionsApiClient and the landing page's <img> pixel embed from malformed input.
    private static string? ValidateFacebookPixelId(string? pixelId)
    {
        return !string.IsNullOrEmpty(pixelId) && pixelId.All(char.IsDigit)
            ? null
            : "invalid_facebook_pixel_id";
    }

    private static IReadOnlyList<DestinationLink> ToLinks(IReadOnlyList<DestinationLinkDto> links) =>
        links.Select(l => new DestinationLink(l.Platform, l.Url)).ToList();

    private static IReadOnlyList<DestinationLinkDto> ToLinkDtos(IReadOnlyList<DestinationLink> links) =>
        links.Select(l => new DestinationLinkDto(l.Platform, l.Url)).ToList();

    private static string? ValidateCoverImage(UploadedFile? file, bool required)
    {
        if (file == null)
            return required ? "cover_image_required" : null;

        if (!AllowedCoverImageContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return "invalid_cover_image_type";

        if (file.Content.Length == 0 || file.Content.Length > MaxCoverImageBytes)
            return "cover_image_too_large";

        return null;
    }

    private async Task<string> StoreCoverImageAsync(Guid releaseId, UploadedFile file)
    {
        var webp = imageProcessor.ToSquareWebp(file.Content, CoverImageSize);
        return await imageStorage.SaveAsync(CoverImageKey(releaseId), webp, "image/webp");
    }

    private static string CoverImageKey(Guid releaseId) => $"{releaseId}/cover.webp";

    private string UrlFor(Release release) => $"{publicSiteSettings.PublicHostname}/{release.Slug}";

    private ReleaseDto ToDto(Release release) =>
        new(
            release.Id.ToString(),
            release.Slug,
            UrlFor(release),
            release.ArtistName,
            release.Title,
            release.Headline,
            release.Description,
            release.CoverImageUrl,
            release.CtaText,
            release.FacebookPixelId,
            ToLinkDtos(release.Links),
            release.Status.ToString(),
            release.CreatedAt);

    private ReleaseSummaryDto ToSummaryDto(Release release) =>
        new(
            release.Id.ToString(),
            release.Title,
            release.Slug,
            UrlFor(release),
            release.Status.ToString(),
            release.CreatedAt);
}
