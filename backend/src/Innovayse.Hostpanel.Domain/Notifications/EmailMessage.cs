using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Notifications;

/// <summary>Represents an email message to be sent.</summary>
/// <param name="To">The recipient email address.</param>
/// <param name="Subject">The email subject line.</param>
/// <param name="Body">The email body content.</param>
/// <param name="IsHtml">Whether the body is HTML. Defaults to <see langword="true"/>.</param>
/// <param name="UnsubscribeUrl">
/// The page that unsubscribes the recipient from this kind of mail, or <see langword="null"/> for a message that is not marketing.
/// When set, the relay adapter marks the message so a mail client can offer an unsubscribe link of its own. It names one
/// recipient by a secret, so it never reaches a log line.
/// </param>
public sealed record EmailMessage(string To, string Subject, string Body, bool IsHtml = true, [property: SecretMember] string? UnsubscribeUrl = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
