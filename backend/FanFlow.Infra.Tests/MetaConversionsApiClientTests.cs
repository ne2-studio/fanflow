using System.Net;
using System.Text.Json;
using FanFlow.Infra;
using FanFlow.Ports.Output;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanFlow.Infra.Tests;

public class MetaConversionsApiClientTests
{
    private const string PixelId = "123456789012345";
    private static readonly NullLogger<MetaConversionsApiClient> Logger = NullLogger<MetaConversionsApiClient>.Instance;

    [Fact]
    public async Task SendConversionEventAsync_ShouldIncludeFbpAndFbc_InUserData_WhenPresent()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

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
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

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
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

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
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

        var conversionEvent = new ConversionEvent(
            EventType.DestinationClick, Guid.NewGuid(), PixelId, "9.8.7.6", "curl/8.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        var userData = UserDataFrom(handler.CapturedBody);
        Assert.Equal("9.8.7.6", userData.GetProperty("client_ip_address").GetString());
        Assert.Equal("curl/8.0", userData.GetProperty("client_user_agent").GetString());
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldOmitTestEventCode_WhenNotConfigured()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

        var conversionEvent = new ConversionEvent(
            EventType.DestinationClick, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        using var json = JsonDocument.Parse(handler.CapturedBody!);
        Assert.False(json.RootElement.TryGetProperty("test_event_code", out _));
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldIncludeTestEventCode_WhenConfigured()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger, "TEST12345");

        var conversionEvent = new ConversionEvent(
            EventType.DestinationClick, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        using var json = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("TEST12345", json.RootElement.GetProperty("test_event_code").GetString());
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldIncludeEventId_WhenPresent()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

        var conversionEvent = new ConversionEvent(
            EventType.PageView, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title",
            MetaEventId: "pv_550e8400-e29b-41d4-a716-446655440000");

        await client.SendConversionEventAsync(conversionEvent);

        using var json = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("pv_550e8400-e29b-41d4-a716-446655440000", json.RootElement.GetProperty("data")[0].GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task SendConversionEventAsync_ShouldOmitEventId_WhenNull()
    {
        var handler = new CapturingHttpMessageHandler();
        var client = new MetaConversionsApiClient(new HttpClient(handler), "token", Logger);

        var conversionEvent = new ConversionEvent(
            EventType.PageView, Guid.NewGuid(), PixelId, "1.2.3.4", "Mozilla/5.0", DateTime.UtcNow, "artist-title");

        await client.SendConversionEventAsync(conversionEvent);

        using var json = JsonDocument.Parse(handler.CapturedBody!);
        Assert.False(json.RootElement.GetProperty("data")[0].TryGetProperty("event_id", out _));
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
