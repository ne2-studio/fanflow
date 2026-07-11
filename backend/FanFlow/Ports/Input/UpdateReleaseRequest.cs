namespace FanFlow.Ports.Input;

/// <summary>
/// Every field is optional — only the fields the caller sets are changed on the release.
/// </summary>
public record UpdateReleaseRequest
(
    string? ArtistName,
    string? Title,
    string? Headline,
    string? Description,
    UploadedFile? CoverImage,
    string? CtaText,
    string? FacebookPixelId,
    IReadOnlyList<DestinationLinkDto>? Links
);
