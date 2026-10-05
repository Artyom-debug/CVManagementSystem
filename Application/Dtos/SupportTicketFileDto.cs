namespace Application.Dtos;

public sealed record SupportTicketFileDto(
    string Summary,
    string ReportedBy,
    string Position,
    string Link,
    string Priority,
    IReadOnlyList<string> AdminEmails);
