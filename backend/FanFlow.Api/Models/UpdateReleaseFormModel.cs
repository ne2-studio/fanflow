using Microsoft.AspNetCore.Http;

namespace FanFlow.Api.Models;

public record UpdateReleaseFormModel(
    string? ArtistName,
    string? Title,
    string? Headline,
    string? Description,
    IFormFile? CoverImage,
    string? CtaText,
    string? FacebookPixelId,
    string? LinksJson);
