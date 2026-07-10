namespace ServiceTemplate.Ports.Input;

public record CreateReleaseRequest
(
    string Title,
    string Headline,
    string Description,
    string CoverImageUrl,
    string BackgroundImageUrl,
    string CtaText,
    IReadOnlyList<DestinationLinkDto> Links
);
