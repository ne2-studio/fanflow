using CSharpFunctionalExtensions;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Tests.Fakes;

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
