namespace Innovayse.Application.Support.Commands.ReplyToMyTicket;

/// <summary>Command for a client to post a reply on one of their own support tickets.</summary>
/// <remarks>
/// Carries no client id and no staff flag. Which account the ticket must belong to is resolved
/// inside the handler from the credential, and the reply is always recorded as a customer reply:
/// a caller able to set that flag would be a caller able to post a message the portal renders as
/// coming from support. The admin route that legitimately replies as staff is a separate use
/// case, <c>ReplyToTicketCommand</c>.
/// </remarks>
/// <param name="TicketId">The ID of the ticket to reply to.</param>
/// <param name="Message">The reply body text.</param>
/// <param name="AuthorName">Display name of the reply author.</param>
public record ReplyToMyTicketCommand(int TicketId, string Message, string AuthorName);
