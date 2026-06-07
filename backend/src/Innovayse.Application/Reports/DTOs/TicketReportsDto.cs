namespace Innovayse.Application.Reports.DTOs;

// ── Ticket Feedback Comments ─────────────────────────────────────────────────

/// <summary>One row in the Ticket Feedback Comments report.</summary>
public record TicketFeedbackCommentRowDto(
    int TicketId,
    string StaffName,
    string Subject,
    string? FeedbackComment,
    int Rating,
    string FeedbackLeftBy,
    DateTimeOffset FeedbackAt);

/// <summary>Ticket Feedback Comments report result.</summary>
public record TicketFeedbackCommentsDto(IReadOnlyList<TicketFeedbackCommentRowDto> Rows);

// ── Ticket Feedback Scores ────────────────────────────────────────────────────

/// <summary>Per-staff feedback score summary with individual rating counts.</summary>
public record TicketFeedbackScoreRowDto(
    string StaffName,
    int R1, int R2, int R3, int R4, int R5,
    int R6, int R7, int R8, int R9, int R10,
    int TotalRatings,
    double AverageRating);

/// <summary>Ticket Feedback Scores report result.</summary>
public record TicketFeedbackScoresDto(IReadOnlyList<TicketFeedbackScoreRowDto> Rows);

// ── Ticket Ratings Reviewer ───────────────────────────────────────────────────

/// <summary>One row in the Ticket Ratings Reviewer report.</summary>
public record TicketRatingRowDto(
    int TicketId,
    DateTimeOffset Date,
    string Message,
    string AdminName,
    int Rating);

/// <summary>Ticket Ratings Reviewer report result.</summary>
public record TicketRatingsReviewerDto(IReadOnlyList<TicketRatingRowDto> Rows);

// ── Ticket Tags ───────────────────────────────────────────────────────────────

/// <summary>One tag with its usage count.</summary>
public record TicketTagRowDto(string Tag, int Count);

/// <summary>Ticket Tags report result.</summary>
public record TicketTagsDto(IReadOnlyList<TicketTagRowDto> Rows);
