namespace FanFlow.Ports.Output;

/// <summary>
/// A Human-classified PageView or DestinationClick, shaped for forwarding to Meta Conversions API.
/// </summary>
public record ConversionEvent
(
    EventType Type,
    Guid ReleaseId,
    string IpAddress,
    string UserAgent,
    DateTime OccurredAt
);
