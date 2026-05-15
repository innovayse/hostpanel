namespace Innovayse.Application.Support.Commands.CloseTicket;

/// <summary>Command to close an open support ticket.</summary>
/// <param name="TicketId">The ID of the ticket to close.</param>
public record CloseTicketCommand(int TicketId);
