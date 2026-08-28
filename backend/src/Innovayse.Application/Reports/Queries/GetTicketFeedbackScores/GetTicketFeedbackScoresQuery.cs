namespace Innovayse.Application.Reports.Queries.GetTicketFeedbackScores;

/// <summary>Query for the Ticket Feedback Scores report.</summary>
/// <param name="From">Earliest feedback date to include.</param>
/// <param name="To">Latest feedback date to include.</param>
public record GetTicketFeedbackScoresQuery(DateOnly? From = null, DateOnly? To = null);
