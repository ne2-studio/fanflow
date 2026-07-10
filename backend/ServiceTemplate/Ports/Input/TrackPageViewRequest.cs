namespace ServiceTemplate.Ports.Input;

public record TrackPageViewRequest
(
    string ReleaseSlug,
    string IpAddress,
    string UserAgent,
    string? Referrer
);
