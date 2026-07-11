namespace FanFlow.Ports.Input;

public record CreateReleaseRequest
(
    string ArtistName,
    string Title,
    string Headline,
    string Description,
    UploadedFile? CoverImage,
    string CtaText,
    string FacebookPixelId,
    IReadOnlyList<DestinationLinkDto> Links
);
