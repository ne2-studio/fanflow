namespace FanFlow.Ports.Input;

public record ReleaseSummaryDto
(
    string Id,
    string Title,
    string Slug,
    string Url,
    string Status,
    DateTime CreatedAt
);
