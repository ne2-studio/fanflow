namespace FanFlow.Api.Models;

public record CreateReleaseRequestModel(
    string Title,
    string Headline,
    string Description,
    string CoverImageUrl,
    string BackgroundImageUrl,
    string CtaText,
    string FacebookPixelId,
    IReadOnlyList<DestinationLinkModel> Links);
