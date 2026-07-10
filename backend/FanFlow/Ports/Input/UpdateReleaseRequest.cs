namespace FanFlow.Ports.Input;

/// <summary>
/// Every field is optional — only the fields the caller sets are changed on the release.
/// </summary>
public record UpdateReleaseRequest
(
    string? Title,
    string? Headline,
    string? Description,
    string? CoverImageUrl,
    string? BackgroundImageUrl,
    string? CtaText,
    string? FacebookPixelId,
    IReadOnlyList<DestinationLinkDto>? Links
);
