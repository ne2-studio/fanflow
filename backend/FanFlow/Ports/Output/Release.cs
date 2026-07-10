namespace FanFlow.Ports.Output;

public record Release
(
    Guid Id,
    string TenantId,
    string Slug,
    string Title,
    string Headline,
    string Description,
    string CoverImageUrl,
    string BackgroundImageUrl,
    string CtaText,
    IReadOnlyList<DestinationLink> Links,
    ReleaseStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
