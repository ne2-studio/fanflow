using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FanFlow.Ports.Output;
using Microsoft.Extensions.Logging;

namespace FanFlow.Infra;

/// <summary>
/// Sends server-side conversion events to the Meta Conversions API
/// (https://developers.facebook.com/docs/marketing-api/conversions-api).
/// </summary>
public class MetaConversionsApiClient(
    HttpClient httpClient,
    string accessToken,
    ILogger<MetaConversionsApiClient> logger,
    string? testEventCode = null) : IConversionsApiClient
{
    public async Task<Result> SendConversionEventAsync(ConversionEvent conversionEvent)
    {
        var eventEntry = new Dictionary<string, object?>
        {
            ["event_name"] = ToMetaEventName(conversionEvent.Type),
            ["event_time"] = new DateTimeOffset(conversionEvent.OccurredAt).ToUnixTimeSeconds(),
            ["action_source"] = "website",
            ["user_data"] = UserData(conversionEvent),
            ["custom_data"] = new { content_name = conversionEvent.ContentName }
        };

        // Omitted (rather than sent null) when absent, same convention as fbp/fbc in UserData —
        // this is the id Meta uses to dedupe this event against the matching browser Pixel event.
        if (!string.IsNullOrWhiteSpace(conversionEvent.MetaEventId))
            eventEntry["event_id"] = conversionEvent.MetaEventId;

        var eventData = new[] { eventEntry };

        object payload = string.IsNullOrWhiteSpace(testEventCode)
            ? new { data = eventData }
            : new { data = eventData, test_event_code = testEventCode };

        var response = await httpClient.PostAsJsonAsync(
            $"https://graph.facebook.com/v25.0/{conversionEvent.PixelId}/events?access_token={accessToken}", payload);

        // Logged without the request URL/token: a 200 only means Meta accepted the HTTP call,
        // the body (events_received, messages) is what confirms the event was actually processed.
        var responseBody = await response.Content.ReadAsStringAsync();
        logger.LogInformation(
            "Meta CAPI response: StatusCode={StatusCode}, Body={ResponseBody}",
            (int)response.StatusCode,
            responseBody);

        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failure("meta_api_error");
    }

    /// <summary>
    /// fbp/fbc are omitted entirely (rather than sent as null/empty) when unavailable, matching
    /// Meta's own recommendation to only include user_data fields that carry a real value.
    /// </summary>
    private static Dictionary<string, string> UserData(ConversionEvent conversionEvent)
    {
        var userData = new Dictionary<string, string>
        {
            ["client_ip_address"] = conversionEvent.IpAddress,
            ["client_user_agent"] = conversionEvent.UserAgent
        };

        if (!string.IsNullOrWhiteSpace(conversionEvent.Fbp))
            userData["fbp"] = conversionEvent.Fbp;

        if (!string.IsNullOrWhiteSpace(conversionEvent.Fbc))
            userData["fbc"] = conversionEvent.Fbc;

        return userData;
    }

    private static string ToMetaEventName(EventType type) => type switch
    {
        EventType.PageView => "PageView",
        EventType.DestinationClick => "SpotifyClick",
        _ => "PageView"
    };
}
