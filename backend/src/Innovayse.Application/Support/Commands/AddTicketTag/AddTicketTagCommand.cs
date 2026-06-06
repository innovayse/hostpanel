namespace Innovayse.Application.Support.Commands.AddTicketTag;

/// <summary>Adds a tag to a ticket.</summary>
public sealed record AddTicketTagCommand(int TicketId, string Tag);
