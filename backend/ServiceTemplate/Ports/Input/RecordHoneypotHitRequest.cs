namespace ServiceTemplate.Ports.Input;

public record RecordHoneypotHitRequest
(
    string ReleaseSlug,
    string IpAddress,
    string UserAgent
);
