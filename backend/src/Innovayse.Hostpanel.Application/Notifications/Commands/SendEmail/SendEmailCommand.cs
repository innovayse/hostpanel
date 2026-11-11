using System.Text;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;

/// <summary>Command to send an email using a registered template.</summary>
/// <param name="To">The recipient email address.</param>
/// <param name="Language">The recipient's language; the template's version in it is used when one exists, the default language's otherwise.</param>
/// <param name="TemplateSlug">The unique slug of the email template to use.</param>
/// <param name="TemplateData">The typed data the template's subject and body render against.</param>
/// <param name="Copy">
/// When set, the same message is also sent to the additional contacts of that client who switched the
/// kind of email on; null sends it to <paramref name="To"/> only.
/// </param>
/// <param name="IdempotencyKey">
/// 64 lower-case hex characters (<see cref="EmailIdempotencyKey"/>) naming this mail; a replayed command
/// with the same key sends each recipient at most once. Null for a synchronous request whose caller
/// waits for the answer and sends a fresh mail on every request.
/// </param>
/// <param name="UnsubscribeUrl">
/// For marketing mail, the page that unsubscribes the recipient; the mail then carries the header a mail client turns into
/// an unsubscribe link. Null for every other mail. Personal to the recipient, so it is never printed.
/// </param>
public sealed record SendEmailCommand(
    string To,
    string? Language,
    string TemplateSlug,
    EmailTemplateModel TemplateData,
    ContactCopy? Copy = null,
    string? IdempotencyKey = null,
    [property: SecretMember] string? UnsubscribeUrl = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
