namespace Innovayse.Application.Reports.Common;

/// <summary>Full Support Ticket Replies report result.</summary>
public record SupportTicketRepliesDto(
    int Month,
    int Year,
    int DaysInMonth,
    IReadOnlyList<SupportTicketRepliesRowDto> Rows);
