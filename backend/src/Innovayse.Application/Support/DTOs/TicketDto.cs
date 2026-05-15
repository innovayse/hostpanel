namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a full support ticket with its replies.</summary>
/// <param name="Id">Ticket primary key.</param>
/// <param name="ClientId">FK to the client who opened the ticket.</param>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Message">The initial message body.</param>
/// <param name="Status">Current lifecycle status as a string.</param>
/// <param name="Priority">Priority level as a string.</param>
/// <param name="DepartmentId">FK to the department this ticket is assigned to, if any.</param>
/// <param name="CreatedAt">UTC timestamp when the ticket was created.</param>
/// <param name="Replies">All replies on this ticket.</param>
public record TicketDto(
    int Id,
    int ClientId,
    string Subject,
    string Message,
    string Status,
    string Priority,
    int? DepartmentId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TicketReplyDto> Replies);
