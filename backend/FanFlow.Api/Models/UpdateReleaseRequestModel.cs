namespace FanFlow.Api.Models;

public record UpdateReleaseRequestModel(
    string? Title,
    string? Headline,
    string? Description,
    string? CoverImageUrl,
    string? BackgroundImageUrl,
    string? CtaText,
    IReadOnlyList<DestinationLinkModel>? Links);
