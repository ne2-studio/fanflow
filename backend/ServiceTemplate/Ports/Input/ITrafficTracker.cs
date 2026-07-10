using CSharpFunctionalExtensions;

namespace ServiceTemplate.Ports.Input;

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
    /// Records the click and returns the destination to redirect to. The redirect always happens
    /// regardless of spam classification, which runs asynchronously afterward.
    /// </summary>
    /// <returns>
    /// The destination URL to redirect to, or a failure: "release_not_found" (idempotent) or
    /// "destination_not_found" (idempotent).
    /// </returns>
    Task<Result<DestinationRedirectDto>> TrackDestinationClickAsync(TrackDestinationClickRequest request);

    /// <summary>
    /// Records a hit on an invisible honeypot link; the event is classified Bot immediately,
    /// synchronously — no async analysis needed for this signal.
    /// </summary>
    /// <returns>
    /// A result indicating success, or a failure: "release_not_found" (idempotent).
    /// </returns>
    Task<Result> RecordHoneypotHitAsync(RecordHoneypotHitRequest request);
}
