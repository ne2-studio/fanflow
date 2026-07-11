using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FanFlow.Ports.Input;

namespace FanFlow.Api.Controllers;

/// <summary>
/// Public, unauthenticated surface hit by the static landing pages (see PRD section 9:
/// tracking routes /pv/*, /out/*, /trap/* proxied to the backend). Rate-limited per the
/// architecture convention for public endpoints. /out/* only records the click — the landing
/// page performs the actual redirect itself, client-side, so it can attempt a native app
/// deep-link before falling back to the web URL.
/// </summary>
[ApiController]
[EnableRateLimiting("PublicLimiter")]
public class TrackingController(ITrafficTracker trafficTracker) : ControllerBase
{
    [HttpGet("pv/{slug}")]
    public async Task<IActionResult> TrackPageView(string slug)
    {
        var result = await trafficTracker.TrackPageViewAsync(new TrackPageViewRequest(
            slug, ClientIp(), UserAgent(), Referrer(), Country(), Fbp(), Fbc()));

        return result.IsSuccess ? NoContent() : NotFound();
    }

    [HttpGet("out/{slug}/{destinationId}")]
    public async Task<IActionResult> TrackDestinationClick(string slug, string destinationId, [FromQuery] int dwell = 0)
    {
        var result = await trafficTracker.TrackDestinationClickAsync(new TrackDestinationClickRequest(
            slug, destinationId, ClientIp(), UserAgent(), Referrer(), dwell, Country(), Fbp(), Fbc()));

        return result.IsSuccess ? NoContent() : NotFound();
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

    // Meta's own _fbp/_fbc cookie values rarely exceed ~60 chars; this cap is a generous
    // sanity bound against malformed/abusive input, not an attempt to validate Meta's format.
    private const int MaxFbTrackingValueLength = 256;

    /// <summary>
    /// The client-supplied ?fbp= query param takes priority over the raw _fbp cookie (the landing
    /// page reads the cookie itself and forwards it explicitly); the cookie is only a fallback for
    /// clients that don't send the query param.
    /// </summary>
    private string? Fbp() => Sanitize(Request.Query["fbp"].FirstOrDefault()) ?? Sanitize(Request.Cookies["_fbp"]);

    private string? Fbc() => Sanitize(Request.Query["fbc"].FirstOrDefault()) ?? Sanitize(Request.Cookies["_fbc"]);

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length > MaxFbTrackingValueLength ? null : trimmed;
    }
}
