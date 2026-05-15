namespace Innovayse.Application.Support.Commands.AssignTicket;

/// <summary>Command to assign a support ticket to a staff member.</summary>
/// <param name="TicketId">The ID of the ticket to assign.</param>
/// <param name="StaffId">FK to the staff member being assigned.</param>
public record AssignTicketCommand(int TicketId, int StaffId);
