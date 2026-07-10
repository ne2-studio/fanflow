using CSharpFunctionalExtensions;

namespace ServiceTemplate.Ports.Input;

/// <summary>
/// Async spam/bot analysis, driven by a background worker polling for unclassified events
/// (see IEventRepository.ListUnclassifiedAsync) — never called synchronously from a tracking
/// request. On a Human classification, forwards the event to Meta Conversions API
/// (IConversionsApiClient); Bot-classified events are never forwarded.
/// </summary>
public interface ISpamClassifier
{
    /// <summary>
    /// Scores and classifies a recorded PageView event using its user agent, request-frequency
    /// history, and (if available) Cloudflare signals.
    /// </summary>
    /// <param name="pageViewId">Id of the unclassified PageView event.</param>
    /// <returns>A result indicating the event was analyzed and updated.</returns>
    Task<Result> AnalyzePageViewAsync(string pageViewId);

    /// <summary>
    /// Scores and classifies a recorded DestinationClick event using its dwell time, user agent,
    /// request-frequency history, and (if available) Cloudflare signals.
    /// </summary>
    /// <param name="clickId">Id of the unclassified DestinationClick event.</param>
    /// <returns>A result indicating the event was analyzed and updated.</returns>
    Task<Result> AnalyzeDestinationClickAsync(string clickId);
}
