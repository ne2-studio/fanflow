namespace ServiceTemplate.Ports.Output;

/// <summary>
/// A single PageView, DestinationClick or HoneypotHit against a release. BotScore/Classification
/// are null until spam analysis has run (HoneypotHit is the exception: it is classified Bot at
/// record time, synchronously).
/// </summary>
public record TrackedEvent
(
    Guid Id,
    Guid ReleaseId,
    EventType Type,
    string IpAddress,
    string UserAgent,
    string? Referrer,
    string? DestinationId,
    int? DwellTimeMs,
    string? Country,
    int? BotScore,
    EventClassification? Classification,
    DateTime CreatedAt
);
