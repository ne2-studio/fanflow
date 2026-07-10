using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FanFlow.Ports.Input;

namespace FanFlow.Api.Controllers;

/// <summary>
/// Public, unauthenticated surface hit by the static landing pages (see PRD section 9:
/// tracking routes /pv/*, /out/*, /trap/* proxied to the backend). Rate-limited per the
/// architecture convention for public endpoints.
/// </summary>
[ApiController]
[EnableRateLimiting("PublicLimiter")]
public class TrackingController(ITrafficTracker trafficTracker) : ControllerBase
{
    [HttpGet("pv/{slug}")]
    public async Task<IActionResult> TrackPageView(string slug)
    {
        var result = await trafficTracker.TrackPageViewAsync(new TrackPageViewRequest(
            slug, ClientIp(), UserAgent(), Referrer(), Country()));

        return result.IsSuccess ? NoContent() : NotFound();
    }

    [HttpGet("out/{slug}/{destinationId}")]
    public async Task<IActionResult> TrackDestinationClick(string slug, string destinationId, [FromQuery] int dwell = 0)
    {
        var result = await trafficTracker.TrackDestinationClickAsync(new TrackDestinationClickRequest(
            slug, destinationId, ClientIp(), UserAgent(), Referrer(), dwell, Country()));

        if (!result.IsSuccess)
            return NotFound();

        return Redirect(result.Value.Url);
    }

    [HttpGet("trap/{slug}")]
    public async Task<IActionResult> RecordHoneypotHit(string slug)
    {
        var result = await trafficTracker.RecordHoneypotHitAsync(new RecordHoneypotHitRequest(
            slug, ClientIp(), UserAgent()));

        return result.IsSuccess ? NoContent() : NotFound();
    }

    private string ClientIp() =>
        Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    private string UserAgent() => Request.Headers.UserAgent.ToString();

    private string? Referrer() =>
        string.IsNullOrWhiteSpace(Request.Headers.Referer.ToString()) ? null : Request.Headers.Referer.ToString();

    private string? Country()
    {
        var country = Request.Headers["CF-IPCountry"].ToString();
        return string.IsNullOrWhiteSpace(country) ? null : country;
    }
}
