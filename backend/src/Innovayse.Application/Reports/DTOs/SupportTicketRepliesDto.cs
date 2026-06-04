namespace Innovayse.Application.Reports.DTOs;

/// <summary>One admin row in the Support Ticket Replies report.</summary>
public record SupportTicketRepliesRowDto(
    string AdminName,
    IReadOnlyList<int> DailyCounts,
    int Total);

/// <summary>Full Support Ticket Replies report result.</summary>
public record SupportTicketRepliesDto(
    int Month,
    int Year,
    int DaysInMonth,
    IReadOnlyList<SupportTicketRepliesRowDto> Rows);
