namespace FanFlow.Ports.Output;

/// <summary>
/// A Human-classified PageView or DestinationClick, shaped for forwarding to Meta Conversions API.
/// </summary>
public record ConversionEvent
(
    EventType Type,
    Guid ReleaseId,
    string PixelId,
    string IpAddress,
    string UserAgent,
    DateTime OccurredAt,
    string ContentName,
    string? Fbp = null,
    string? Fbc = null
);
