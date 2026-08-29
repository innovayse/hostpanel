namespace Innovayse.Application.Reports.Common;

/// <summary>One row in the Ticket Ratings Reviewer report.</summary>
/// <param name="TicketId">Identifier of the rated ticket.</param>
/// <param name="Date">When the rated reply was posted.</param>
/// <param name="Message">Body of the reply that was rated.</param>
/// <param name="AdminName">Admin who wrote the reply.</param>
/// <param name="Rating">Score the client gave the reply.</param>
public record TicketRatingRowDto(
    int TicketId,
    DateTimeOffset Date,
    string Message,
    string AdminName,
    int Rating);
