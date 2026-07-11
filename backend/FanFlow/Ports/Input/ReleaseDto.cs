namespace FanFlow.Ports.Input;

public record ReleaseDto
(
    string Id,
    string Slug,
    string Url,
    string ArtistName,
    string Title,
    string Headline,
    string Description,
    string CoverImageUrl,
    string CtaText,
    string FacebookPixelId,
    IReadOnlyList<DestinationLinkDto> Links,
    string Status,
    DateTime CreatedAt
);
