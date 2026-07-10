namespace FanFlow.Ports.Output;

public record Release
(
    Guid Id,
    string TenantId,
    string Slug,
    string ArtistName,
    string Title,
    string Headline,
    string Description,
    string CoverImageUrl,
    string BackgroundImageUrl,
    string CtaText,
    string FacebookPixelId,
    IReadOnlyList<DestinationLink> Links,
    ReleaseStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
