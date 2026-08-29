namespace Innovayse.Application.Support.Common;

/// <summary>
/// Thrown when a client-facing ticket use case is asked for a ticket the caller may not have.
/// <para>
/// One type deliberately covers three situations: the ticket belongs to another client, no
/// ticket with that id exists at all, and the caller has no client record. Ticket ids are
/// sequential integers, so answering them apart -- 403 here, 404 there -- would turn
/// <c>/api/me/tickets/{id}</c> into a way of finding out which ids exist and which of them are
/// somebody else's. All three answer 404 carrying <see cref="Code"/>.
/// </para>
/// <para>
/// It is a distinct type rather than an <see cref="InvalidOperationException"/> so the API layer
/// can answer 404 with a code the browser branches on, instead of a 400 carrying an English
/// sentence the frontend would have to string-match.
/// </para>
/// </summary>
/// <param name="ticketId">The ticket id that was asked for. For the server-side log only.</param>
public sealed class TicketNotFoundException(int ticketId) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "TICKET_NOT_FOUND";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PublicMessage"/> is still the English text and is still what
    /// <see cref="System.Exception.Message"/> carries, so a log line and a test read the same
    /// sentence they always did. What the caller is shown is looked up under this key instead,
    /// because the portal ships in en/ru/hy and a customer reading Russian or Armenian was
    /// previously served this English constant for every failure the frontend had no entry for.
    /// </remarks>
    public const string MessageKey = "TicketNotFound";

    /// <summary>
    /// The sentence the caller is shown. It deliberately says nothing about whether the ticket
    /// exists or who it belongs to -- that is the fact this type exists to withhold.
    /// </summary>
    public const string PublicMessage = "No such ticket.";

    /// <summary>
    /// The ticket id that was asked for. Logged server-side; never written to the response body,
    /// because echoing it back tells the caller their probe was understood.
    /// </summary>
    public int TicketId { get; } = ticketId;
}
