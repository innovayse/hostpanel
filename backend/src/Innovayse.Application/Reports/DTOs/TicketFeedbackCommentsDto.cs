namespace Innovayse.Application.Reports.DTOs;

/// <summary>Ticket Feedback Comments report result.</summary>
/// <param name="Rows">One entry per piece of feedback matching the filters.</param>
public record TicketFeedbackCommentsDto(IReadOnlyList<TicketFeedbackCommentRowDto> Rows);
