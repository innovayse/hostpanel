namespace Innovayse.Application.Support.Commands.ReplyToTicket;

/// <summary>Command to post a reply on an existing support ticket.</summary>
/// <param name="TicketId">The ID of the ticket to reply to.</param>
/// <param name="Message">The reply body text.</param>
/// <param name="AuthorName">Display name of the reply author.</param>
/// <param name="IsStaffReply">Whether the author is a staff member.</param>
public record ReplyToTicketCommand(int TicketId, string Message, string AuthorName, bool IsStaffReply);
