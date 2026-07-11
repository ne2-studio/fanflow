using CSharpFunctionalExtensions;

namespace FanFlow.Ports.Input;

/// <summary>
/// Public, unauthenticated tracking surface hit by the static landing pages (/pv/*, /out/*,
/// /trap/*). Purely records events with the data needed for later spam analysis — no bot
/// scoring happens synchronously here, see ISpamClassifier.
/// </summary>
public interface ITrafficTracker
{
    /// <returns>
    /// A result indicating success, or a failure: "release_not_found" (idempotent).
    /// </returns>
    Task<Result> TrackPageViewAsync(TrackPageViewRequest request);

    /// <summary>
    /// Records the click. The landing page performs the redirect itself, client-side (native app
    /// deep-link attempt with a web fallback) — this only needs to record the event, regardless of
    /// spam classification, which runs asynchronously afterward.
    /// </summary>
    /// <returns>
    /// A result indicating success, or a failure: "release_not_found" (idempotent) or
    /// "destination_not_found" (idempotent).
    /// </returns>
    Task<Result> TrackDestinationClickAsync(TrackDestinationClickRequest request);

    /// <summary>
    /// Records a hit on an invisible honeypot link; the event is classified Bot immediately,
    /// synchronously — no async analysis needed for this signal.
    /// </summary>
    /// <returns>
    /// A result indicating success, or a failure: "release_not_found" (idempotent).
    /// </returns>
    Task<Result> RecordHoneypotHitAsync(RecordHoneypotHitRequest request);
}
