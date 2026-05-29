namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a full support ticket with its replies.</summary>
/// <param name="Id">Ticket primary key.</param>
/// <param name="ClientId">FK to the client who opened the ticket.</param>
/// <param name="Subject">The ticket subject line.</param>
/// <param name="Message">The initial message body.</param>
/// <param name="Status">Current lifecycle status as a string.</param>
/// <param name="Priority">Priority level as a string.</param>
/// <param name="DepartmentId">FK to the department this ticket is assigned to, if any.</param>
/// <param name="DepartmentName">Name of the assigned department, if any.</param>
/// <param name="AssignedToStaffId">FK to the staff member assigned, if any.</param>
/// <param name="AssignedToStaffName">Display name of the assigned staff member, if any.</param>
/// <param name="CreatedAt">UTC timestamp when the ticket was created.</param>
/// <param name="IsFlagged">Whether this ticket has been flagged by staff.</param>
/// <param name="Replies">All replies on this ticket.</param>
public record TicketDto(
    int Id,
    int ClientId,
    string Subject,
    string Message,
    string Status,
    string Priority,
    int? DepartmentId,
    string? DepartmentName,
    int? AssignedToStaffId,
    string? AssignedToStaffName,
    DateTimeOffset CreatedAt,
    bool IsFlagged,
    IReadOnlyList<TicketReplyDto> Replies);
