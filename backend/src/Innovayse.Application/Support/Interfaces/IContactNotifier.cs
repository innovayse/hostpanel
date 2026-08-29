namespace Innovayse.Application.Support.Interfaces;

using Innovayse.Application.Support.Common;

/// <summary>
/// Posts a contact-form enquiry to the operator's own chat, beside the mail the enquiry inbox
/// receives.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not named for a chat product. The one implementation today is Telegram, but this
/// layer's interest is "the operator is told about this quickly, where they already are" -- the
/// escaping, the parse mode and the endpoint are the transport's problem. Swapping the channel is
/// then a registration rather than an edit to a use case, and nothing in Application names a
/// vendor it cannot reach.
/// </para>
/// <para>
/// <b>This channel is best-effort, and the port says so rather than leaving each caller to
/// guess.</b> The mail is the delivery the operator's process is built on: an enquiry whose mail
/// was accepted has arrived, and a chat outage must never turn it into a failure the visitor is
/// shown. The Nuxt route this moved from had that rule written in its header, and it travelled
/// with the code.
/// </para>
/// <para>
/// What that costs is silence, so it is paid for in the log instead: an implementation that is
/// not configured, and one whose post fails, both say so. A channel that quietly does nothing is
/// the defect the contact form was rewritten to remove, in a smaller form.
/// </para>
/// </remarks>
public interface IContactNotifier
{
    /// <summary>Posts one enquiry to the operator's chat.</summary>
    /// <param name="enquiry">The validated submission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A task that completes once the attempt is over. Completing does <b>not</b> mean the chat
    /// received the post: an implementation this deployment did not configure completes normally,
    /// having logged that it did nothing.
    /// </returns>
    /// <exception cref="Exception">
    /// An implementation may throw when the transport fails, reporting the cause faithfully
    /// rather than hiding it. Deciding that a failed post is survivable belongs to the caller that
    /// knows whether the mail was already delivered -- <c>SendContactMessageHandler</c> -- not to
    /// the channel, which cannot see that.
    /// </exception>
    Task NotifyAsync(ContactEnquiry enquiry, CancellationToken ct);
}
