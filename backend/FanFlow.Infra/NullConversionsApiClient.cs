using CSharpFunctionalExtensions;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

/// <summary>
/// No-op stand-in for MetaConversionsApiClient, used when Meta credentials aren't configured
/// (dev/test environments). Toggled via "Features:MetaConversions:Enabled" in ServiceRegistration.
/// </summary>
public class NullConversionsApiClient : IConversionsApiClient
{
    public Task<Result> SendConversionEventAsync(ConversionEvent conversionEvent)
    {
        return Task.FromResult(Result.Success());
    }
}
