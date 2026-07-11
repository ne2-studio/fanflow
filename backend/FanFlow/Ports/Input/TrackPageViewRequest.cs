namespace FanFlow.Ports.Input;

public record TrackPageViewRequest
(
    string ReleaseSlug,
    string IpAddress,
    string UserAgent,
    string? Referrer,
    string? Country,
    string? Fbp = null,
    string? Fbc = null
);
