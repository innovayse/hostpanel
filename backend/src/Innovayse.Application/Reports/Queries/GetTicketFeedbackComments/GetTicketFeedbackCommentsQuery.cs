namespace Innovayse.Application.Reports.Queries.GetTicketFeedbackComments;

/// <summary>Query for the Ticket Feedback Comments report.</summary>
/// <param name="StaffName">Admin to narrow to, or null for every admin.</param>
/// <param name="From">Earliest feedback date to include.</param>
/// <param name="To">Latest feedback date to include.</param>
public record GetTicketFeedbackCommentsQuery(
    string? StaffName = null,
    DateOnly? From = null,
    DateOnly? To = null);
