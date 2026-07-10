using CSharpFunctionalExtensions;
using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class SpyConversionsApiClient : IConversionsApiClient
{
    public List<ConversionEvent> SentEvents { get; } = new();
    public bool ShouldFail { get; set; }

    public Task<Result> SendConversionEventAsync(ConversionEvent conversionEvent)
    {
        SentEvents.Add(conversionEvent);
        return Task.FromResult(ShouldFail ? Result.Failure("meta_api_error") : Result.Success());
    }
}
