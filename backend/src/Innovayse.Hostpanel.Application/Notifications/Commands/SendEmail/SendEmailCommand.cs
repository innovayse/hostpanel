using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

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
public sealed record SendEmailCommand(
    string To,
    string? Language,
    string TemplateSlug,
    EmailTemplateModel TemplateData,
    ContactCopy? Copy = null,
    string? IdempotencyKey = null);
