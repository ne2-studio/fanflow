using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FanFlow.Api.Models;
using FanFlow.Ports.Input;

namespace FanFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/releases")]
public class ReleasesController(IReleaseManager releaseManager, IReleaseAnalytics releaseAnalytics) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateReleaseFormModel request)
    {
        var result = await releaseManager.CreateAsync(new CreateReleaseRequest(
            request.ArtistName,
            request.Title,
            request.Headline,
            request.Description,
            await ToUploadedFileAsync(request.CoverImage),
            request.CtaText,
            request.FacebookPixelId,
            ToLinkDtos(DeserializeLinks(request.LinksJson))));

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(string id, [FromForm] UpdateReleaseFormModel request)
    {
        var links = request.LinksJson == null ? null : DeserializeLinks(request.LinksJson);

        var result = await releaseManager.UpdateAsync(id, new UpdateReleaseRequest(
            request.ArtistName,
            request.Title,
            request.Headline,
            request.Description,
            await ToUploadedFileAsync(request.CoverImage),
            request.CtaText,
            request.FacebookPixelId,
            links == null ? null : ToLinkDtos(links)));

        if (!result.IsSuccess)
            return MapFailure(result.Error);

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await releaseManager.ListAsync();

        if (!result.IsSuccess)
            return StatusCode(500, new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var result = await releaseManager.GetAsync(id);

        if (!result.IsSuccess)
            return MapFailure(result.Error);

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await releaseManager.DeleteAsync(id);

        if (!result.IsSuccess)
            return MapFailure(result.Error);

        return NoContent();
    }

    [HttpGet("{id}/analytics")]
    public async Task<IActionResult> GetAnalytics(string id, [FromQuery] string filter = "all")
    {
        var trafficFilter = string.Equals(filter, "human", StringComparison.OrdinalIgnoreCase)
            ? TrafficFilter.HumanOnly
            : TrafficFilter.All;

        var result = await releaseAnalytics.GetAsync(id, trafficFilter);

        if (!result.IsSuccess)
            return MapFailure(result.Error);

        return Ok(result.Value);
    }

    private static IReadOnlyList<DestinationLinkDto> ToLinkDtos(IReadOnlyList<DestinationLinkModel> links) =>
        links.Select(l => new DestinationLinkDto(l.Platform, l.Url)).ToList();

    private static readonly JsonSerializerOptions LinksJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static IReadOnlyList<DestinationLinkModel> DeserializeLinks(string linksJson) =>
        JsonSerializer.Deserialize<List<DestinationLinkModel>>(linksJson, LinksJsonOptions) ?? [];

    private static async Task<UploadedFile?> ToUploadedFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        return new UploadedFile(file.ContentType, stream.ToArray());
    }

    private IActionResult MapFailure(string error) =>
        error == "release_not_found" ? NotFound(new { error }) : BadRequest(new { error });
}
