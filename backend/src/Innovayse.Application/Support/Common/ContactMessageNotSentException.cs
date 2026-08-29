namespace Innovayse.Application.Support.Common;

/// <summary>
/// Thrown when the public contact form was well-formed and the recipient configured, but the
/// SMTP relay refused or never answered.
/// <para>
/// A distinct type rather than letting the MailKit exception reach the last handler in
/// <c>ExceptionMiddleware</c>: that one deliberately answers a bare 500 with
/// <c>INTERNAL_ERROR</c> and no detail, which tells the visitor nothing about whether trying
/// again is worth it. This one says the delivery failed, carries <see cref="Code"/> so the page
/// can offer a retry, and keeps the relay's own message -- host names, credentials, and
/// certificate errors among them -- server-side.
/// </para>
/// <para>
/// The transport failure is passed as the inner exception so the log line carries the real
/// cause; it is never written to the response body.
/// </para>
/// </summary>
/// <param name="inner">The transport failure that caused this. For the server-side log only.</param>
public sealed class ContactMessageNotSentException(Exception inner) : Exception(PublicMessage, inner)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "CONTACT_SEND_FAILED";

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
    public const string MessageKey = "ContactSendFailed";

    /// <summary>
    /// The sentence the visitor is shown. It says the message did <b>not</b> arrive and invites a
    /// retry, which is the honest answer for a relay that was reachable a minute ago.
    /// </summary>
    public const string PublicMessage =
        "Your message could not be delivered. Please try again in a few minutes.";
}
