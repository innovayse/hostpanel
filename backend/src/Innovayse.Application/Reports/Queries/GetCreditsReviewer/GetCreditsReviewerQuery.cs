namespace Innovayse.Application.Reports.Queries.GetCreditsReviewer;

/// <summary>Query for the Credits Reviewer report.</summary>
/// <param name="ClientId">Client to narrow to, or null for every client.</param>
/// <param name="From">Earliest credit date to include.</param>
/// <param name="To">Latest credit date to include.</param>
/// <param name="MinAmount">Smallest credit amount to include.</param>
/// <param name="MaxAmount">Largest credit amount to include.</param>
public record GetCreditsReviewerQuery(
    int? ClientId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null);
