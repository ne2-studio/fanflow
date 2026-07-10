namespace ServiceTemplate.Ports.Input;

public record ReleaseSummaryDto
(
    string Id,
    string Title,
    string Slug,
    string Status,
    DateTime CreatedAt
);
