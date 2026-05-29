namespace Innovayse.Application.Support.Commands.UpdateTicket;

/// <summary>Command to update a ticket's metadata (status, priority, department, assignment).</summary>
/// <param name="TicketId">The ticket identifier.</param>
/// <param name="Status">New status as a string, or <see langword="null"/> to leave unchanged.</param>
/// <param name="Priority">New priority as a string, or <see langword="null"/> to leave unchanged.</param>
/// <param name="DepartmentId">New department FK, or <see langword="null"/> to leave unchanged.</param>
/// <param name="AssignedToStaffId">New staff assignment FK, or <see langword="null"/> to leave unchanged.</param>
public record UpdateTicketCommand(
    int TicketId,
    string? Status,
    string? Priority,
    int? DepartmentId,
    int? AssignedToStaffId);
