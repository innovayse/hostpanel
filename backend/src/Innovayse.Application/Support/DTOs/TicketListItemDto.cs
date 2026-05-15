namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a summary ticket row for list views.</summary>
/// <param name="Id">Ticket primary key.</param>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Status">Current lifecycle status as a string.</param>
/// <param name="Priority">Priority level as a string.</param>
/// <param name="CreatedAt">UTC timestamp when the ticket was created.</param>
/// <param name="ReplyCount">Total number of replies on this ticket.</param>
public record TicketListItemDto(
    int Id,
    string Subject,
    string Status,
    string Priority,
    DateTimeOffset CreatedAt,
    int ReplyCount);
