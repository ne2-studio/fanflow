namespace FanFlow.Api.Models;

public record UpdateReleaseRequestModel(
    string? Title,
    string? Headline,
    string? Description,
    string? CoverImageUrl,
    string? BackgroundImageUrl,
    string? CtaText,
    string? FacebookPixelId,
    IReadOnlyList<DestinationLinkModel>? Links);
