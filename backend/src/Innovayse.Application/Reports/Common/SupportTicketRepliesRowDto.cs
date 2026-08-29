namespace Innovayse.Application.Reports.Common;

/// <summary>One admin row in the Support Ticket Replies report.</summary>
public record SupportTicketRepliesRowDto(
    string AdminName,
    IReadOnlyList<int> DailyCounts,
    int Total);
