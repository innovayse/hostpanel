namespace Innovayse.Application.Reports.Queries.GetTicketRatingsReviewer;

/// <summary>Query for the Ticket Ratings Reviewer report.</summary>
/// <param name="MinRating">Lowest rating to include, or null for every rating.</param>
/// <param name="From">Earliest reply date to include.</param>
/// <param name="To">Latest reply date to include.</param>
public record GetTicketRatingsReviewerQuery(
    int? MinRating = null,
    DateOnly? From = null,
    DateOnly? To = null);
