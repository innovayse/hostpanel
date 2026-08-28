namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Ticket Feedback Comments report.</summary>
/// <param name="TicketId">Identifier of the ticket the feedback was left against.</param>
/// <param name="StaffName">Admin who handled the ticket.</param>
/// <param name="Subject">Ticket subject line.</param>
/// <param name="FeedbackComment">Free-text comment left by the client, if any.</param>
/// <param name="Rating">Score the client gave, on the report's 1-10 scale.</param>
/// <param name="FeedbackLeftBy">Name of the client who left the feedback.</param>
/// <param name="FeedbackAt">When the feedback was submitted.</param>
public record TicketFeedbackCommentRowDto(
    int TicketId,
    string StaffName,
    string Subject,
    string? FeedbackComment,
    int Rating,
    string FeedbackLeftBy,
    DateTimeOffset FeedbackAt);
