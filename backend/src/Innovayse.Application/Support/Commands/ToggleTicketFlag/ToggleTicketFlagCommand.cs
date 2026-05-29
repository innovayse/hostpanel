namespace Innovayse.Application.Support.Commands.ToggleTicketFlag;

/// <summary>Command to toggle the flagged state of a support ticket.</summary>
/// <param name="TicketId">The ticket identifier.</param>
public record ToggleTicketFlagCommand(int TicketId);
