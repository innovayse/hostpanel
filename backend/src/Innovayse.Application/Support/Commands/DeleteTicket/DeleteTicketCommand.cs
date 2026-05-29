namespace Innovayse.Application.Support.Commands.DeleteTicket;

/// <summary>Command to permanently delete a support ticket.</summary>
/// <param name="TicketId">The ticket identifier.</param>
public record DeleteTicketCommand(int TicketId);
