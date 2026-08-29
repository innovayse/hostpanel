namespace Innovayse.Application.Reports.Common;

/// <summary>Ticket Feedback Scores report result.</summary>
/// <param name="Rows">One entry per admin who received feedback in the period.</param>
public record TicketFeedbackScoresDto(IReadOnlyList<TicketFeedbackScoreRowDto> Rows);
