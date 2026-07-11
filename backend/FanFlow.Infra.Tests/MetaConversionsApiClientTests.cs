using System.Net;
using System.Text.Json;
using FanFlow.Infra;
using FanFlow.Ports.Output;

namespace FanFlow.Infra.Tests;

public class MetaConversionsApiClientTests
{
    private const string PixelId = "123456789012345";

    [Fact]
    public async Task SendConversionEventAsync_ShouldIncludeFbpAndFbc_InUserData_WhenPresent()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token");

        var conversionEvent = new ConversionEvent(
            EventType.PageView, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title",
            "fb.1.111.abc", "fb.1.222.xyz");

        await client.SendConversionEventAsync(conversionEvent);

        var userData = UserDataFrom(handler.CapturedBody);
        Assert.Equal("fb.1.111.abc", userData.GetProperty("fbp").GetString());
        Assert.Equal("fb.1.222.xyz", userData.GetProperty("fbc").GetString());
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldOmitFbpAndFbc_FromUserData_WhenNull()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token");

        var conversionEvent = new ConversionEvent(
            EventType.PageView, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        var userData = UserDataFrom(handler.CapturedBody);
        Assert.False(userData.TryGetProperty("fbp", out _));
        Assert.False(userData.TryGetProperty("fbc", out _));
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldOmitFbpAndFbc_FromUserData_WhenEmpty()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token");

        var conversionEvent = new ConversionEvent(
            EventType.PageView, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title", "", "  ");

        await client.SendConversionEventAsync(conversionEvent);

        var userData = UserDataFrom(handler.CapturedBody);
        Assert.False(userData.TryGetProperty("fbp", out _));
        Assert.False(userData.TryGetProperty("fbc", out _));
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldStillIncludeIpAndUserAgent_RegardlessOfFbpFbc()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token");

        var conversionEvent = new ConversionEvent(
            EventType.DestinationClick, Guid.NewGuid(), PixelId, "9.8.7.6", "curl/8.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        var userData = UserDataFrom(handler.CapturedBody);
        Assert.Equal("9.8.7.6", userData.GetProperty("client_ip_address").GetString());
        Assert.Equal("curl/8.0", userData.GetProperty("client_user_agent").GetString());
    }

    private static JsonElement UserDataFrom(string? body)
    {
        Assert.NotNull(body);
        using var json = JsonDocument.Parse(body!);
        return json.RootElement.GetProperty("data")[0].GetProperty("user_data").Clone();
    }

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedBody = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
