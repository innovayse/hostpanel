namespace Innovayse.Application.Support.Commands.RemoveTicketTag;

/// <summary>Removes a tag from a ticket.</summary>
public sealed record RemoveTicketTagCommand(int TicketId, string Tag);
