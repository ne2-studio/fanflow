using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

/// <summary>
/// Sends server-side conversion events to the Meta Conversions API
/// (https://developers.facebook.com/docs/marketing-api/conversions-api).
/// </summary>
public class MetaConversionsApiClient(HttpClient httpClient, string pixelId, string accessToken) : IConversionsApiClient
{
    public async Task<Result> SendConversionEventAsync(ConversionEvent conversionEvent)
    {
        var payload = new
        {
            data = new[]
            {
                new
                {
                    event_name = ToMetaEventName(conversionEvent.Type),
                    event_time = new DateTimeOffset(conversionEvent.OccurredAt).ToUnixTimeSeconds(),
                    action_source = "website",
                    user_data = new
                    {
                        client_ip_address = conversionEvent.IpAddress,
                        client_user_agent = conversionEvent.UserAgent
                    }
                }
            }
        };

        var response = await httpClient.PostAsJsonAsync(
            $"https://graph.facebook.com/v18.0/{pixelId}/events?access_token={accessToken}", payload);

        return response.IsSuccessStatusCode
            ? Result.Success()
            : Result.Failure("meta_api_error");
    }

    private static string ToMetaEventName(EventType type) => type switch
    {
        EventType.PageView => "PageView",
        EventType.DestinationClick => "SpotifyClick",
        _ => "PageView"
    };
}
