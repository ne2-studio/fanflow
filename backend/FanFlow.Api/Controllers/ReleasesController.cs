using Microsoft.AspNetCore.Authorization;
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
    public async Task<IActionResult> Create([FromBody] CreateReleaseRequestModel request)
    {
        var result = await releaseManager.CreateAsync(new CreateReleaseRequest(
            request.Title,
            request.Headline,
            request.Description,
            request.CoverImageUrl,
            request.BackgroundImageUrl,
            request.CtaText,
            request.FacebookPixelId,
            ToLinkDtos(request.Links)));

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateReleaseRequestModel request)
    {
        var result = await releaseManager.UpdateAsync(id, new UpdateReleaseRequest(
            request.Title,
            request.Headline,
            request.Description,
            request.CoverImageUrl,
            request.BackgroundImageUrl,
            request.CtaText,
            request.FacebookPixelId,
            request.Links == null ? null : ToLinkDtos(request.Links)));

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

    private IActionResult MapFailure(string error) =>
        error == "release_not_found" ? NotFound(new { error }) : BadRequest(new { error });
}
