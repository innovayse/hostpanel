namespace Innovayse.Application.Reports.Common;

/// <summary>Ticket Ratings Reviewer report result.</summary>
/// <param name="Rows">One entry per rated reply matching the filters.</param>
public record TicketRatingsReviewerDto(IReadOnlyList<TicketRatingRowDto> Rows);
