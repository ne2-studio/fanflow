namespace FanFlow.Ports.Input;

// Shape returned by GET /api/releases/{id}/events (ListReleaseEvents). BotScore/Classification
// are null until async spam analysis has run.
public record TrackedEventDto(
    string Id,
    string Type,
    string IpAddress,
    string UserAgent,
    string? Referrer,
    string? DestinationId,
    int? DwellTimeMs,
    string? Country,
    int? BotScore,
    string? Classification,
    DateTime CreatedAt,
    string? Fbp,
    string? Fbc,
    string? MetaEventId);
