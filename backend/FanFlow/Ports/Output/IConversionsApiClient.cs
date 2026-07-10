using CSharpFunctionalExtensions;

namespace FanFlow.Ports.Output;

/// <summary>
/// Sends server-side conversion events to Meta Conversions API. Only ever called for
/// Human-classified events, after async spam analysis has completed.
/// </summary>
public interface IConversionsApiClient
{
    /// <returns>
    /// A result indicating success, or a failure ("meta_api_error") if Meta's API is unreachable
    /// or rejects the event.
    /// </returns>
    Task<Result> SendConversionEventAsync(ConversionEvent conversionEvent);
}
