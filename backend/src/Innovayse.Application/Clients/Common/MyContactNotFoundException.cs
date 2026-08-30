namespace Innovayse.Application.Clients.Common;

/// <summary>
/// Thrown when a contact use case is asked for a contact the caller's own account does not have.
/// <para>
/// One type deliberately covers two situations: the contact belongs to another client, and no
/// contact with that id exists at all. Contact ids are sequential integers, so answering them
/// apart -- or naming the id back in the sentence -- would turn
/// <c>/api/clients/me/contacts/{contactId}</c> into a way of finding out which ids are real and
/// which of them are somebody else's. Both answer 404 carrying <see cref="Code"/>, with the same
/// body byte for byte.
/// </para>
/// <para>
/// It replaces the bare <see cref="InvalidOperationException"/> the aggregate raises
/// (<c>Contact {id} not found.</c>), which landed in <c>ExceptionMiddleware</c>'s unclassified
/// 400 bin as <c>INVALID_OPERATION</c> and carried a hardcoded English sentence with the probed
/// id in it. Shaped like <c>MyServiceNotFoundException</c>, <c>DomainNotFoundException</c>,
/// <c>TicketNotFoundException</c> and <c>InvoiceNotFoundException</c> instead, so every
/// client-facing resource addressed by a sequential id refuses alike.
/// </para>
/// <para>
/// The aggregate keeps its own guard: <see cref="Innovayse.Domain.Clients.Client.UpdateContact"/>
/// and <see cref="Innovayse.Domain.Clients.Client.RemoveContact"/> still throw for an unknown
/// contact, because a domain invariant may not depend on a caller having checked first. The
/// handlers check ahead of them so the refusal that reaches the wire is this one.
/// </para>
/// </summary>
/// <param name="contactId">The contact id that was asked for. For the server-side log only.</param>
public sealed class MyContactNotFoundException(int contactId) : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "MY_CONTACT_NOT_FOUND";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PublicMessage"/> is still the English text and is still what
    /// <see cref="System.Exception.Message"/> carries, so a log line and a test read one sentence.
    /// What the caller is shown is looked up under this key instead, because the portal ships in
    /// en/ru/hy and the sentence this replaces was English for all three.
    /// </remarks>
    public const string MessageKey = "MyContactNotFound";

    /// <summary>
    /// The sentence the caller is shown. It deliberately says nothing about whether the contact
    /// exists or who it belongs to -- that is the fact this type exists to withhold -- and it
    /// carries no id.
    /// </summary>
    public const string PublicMessage = "No such contact.";

    /// <summary>
    /// The contact id that was asked for. Logged server-side; never written to the response body,
    /// because echoing it back tells the caller their probe was understood.
    /// </summary>
    public int ContactId { get; } = contactId;
}
