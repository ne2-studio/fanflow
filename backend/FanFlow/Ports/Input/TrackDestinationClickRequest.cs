namespace FanFlow.Ports.Input;

public record TrackDestinationClickRequest
(
    string ReleaseSlug,
    string DestinationId,
    string IpAddress,
    string UserAgent,
    string? Referrer,
    int DwellTimeMs,
    string? Country,
    string? Fbp = null,
    string? Fbc = null
);
