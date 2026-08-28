namespace Innovayse.Application.Reports.Queries.GetSupportTicketReplies;

/// <summary>Query for the Support Ticket Replies report, counted per admin per day.</summary>
/// <param name="Year">Calendar year, or null for the current UTC year.</param>
/// <param name="Month">Calendar month 1-12, or null for the current UTC month.</param>
public record GetSupportTicketRepliesQuery(int? Year = null, int? Month = null);
