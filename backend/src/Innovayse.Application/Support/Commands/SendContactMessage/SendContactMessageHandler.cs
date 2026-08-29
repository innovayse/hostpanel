namespace Innovayse.Application.Support.Commands.SendContactMessage;

using System.Net;
using System.Text;
using Innovayse.Application.Notifications.Options;
using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.Interfaces;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Application.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Relays one public contact-form submission to the operator's enquiry inbox through
/// <see cref="IEmailSender"/>, then announces it on the operator's chat through
/// <see cref="IContactNotifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// This use case is the reason the platform no longer has two SMTP clients. The public site's
/// Nuxt server used to hold the relay credentials and send this mail itself, with its own TLS
/// decision and its own failure behaviour; the mail account's password lived in a second
/// container that had no other reason to hold it. The frontend now posts here and the one
/// configured relay -- <c>MailKitEmailSender</c> -- sends everything this platform sends.
/// </para>
/// <para>
/// <b>The mail is not best-effort.</b> Every way it can fail throws, and the API layer turns each
/// into a status and a code. The route this replaced skipped the send when its configuration was
/// incomplete and returned success regardless, so an operator who filled in none of it had a
/// contact form that told every visitor their message had arrived and delivered none of them,
/// with nothing in any log. A caller of this handler that gets no exception can rely on the relay
/// having accepted the message.
/// </para>
/// <para>
/// <b>The chat post is</b>, and only it. It happens after the mail and its failure is logged and
/// dropped: the enquiry inbox is the delivery the operator's process is built on, so an enquiry
/// whose mail was accepted has arrived and a Telegram outage must not report it as lost. That is
/// the same rule, in the same order, that the Nuxt route enforced while it held the bot token,
/// and it is not the silent-success bug -- nothing is skipped on a configuration check without
/// being logged, and the outcome the visitor is told about is still the one that was earned.
/// </para>
/// <para>
/// Deliberately not routed through <c>SendEmailCommand</c>. That path renders a database-seeded
/// template and, having written an <c>EmailLog</c> row, <b>swallows the send failure and returns
/// normally</b> -- which is the exact behaviour this change exists to remove. It would also need
/// a new seeded template and the migration that carries it, for a body with no operator-editable
/// content in it.
/// </para>
/// <para>
/// No FluentValidation validator accompanies this command, and that is still deliberate -- but the
/// reason has changed. The pipeline is now wired: <c>Program.cs</c> calls
/// <c>opts.UseFluentValidation()</c> and registers every validator in this assembly, so a rule
/// written in one <b>would</b> run. The checks stay in the handler because they are not all
/// field-shape rules: the length caps are trimmed and re-checked against the same values the mail
/// body is built from, and the recipient check reads configuration and refuses with
/// <c>ContactRecipientNotConfiguredException</c>, which carries a 503 and a code of its own. A
/// validator can only answer 400 <c>VALIDATION_FAILED</c>, so moving that check would lose the
/// distinction between "you sent something wrong" and "this deployment cannot accept contact mail".
/// </para>
/// </remarks>
/// <param name="emailSender">The one configured SMTP relay.</param>
/// <param name="contactNotifier">The operator's chat channel, notified after the mail is away.</param>
/// <param name="options">Notification recipients, including the enquiry inbox.</param>
/// <param name="logger">Logger, for the failures an operator has to see.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
public sealed class SendContactMessageHandler(
    IEmailSender emailSender,
    IContactNotifier contactNotifier,
    IOptions<NotificationOptions> options,
    ILogger<SendContactMessageHandler> logger,
    IStringLocalizer<ValidationMessages> localizer)
{
    /// <summary>
    /// Longest accepted value for the short single-line fields, in characters.
    /// The endpoint is anonymous and reachable without the page, so the caps are enforced here
    /// rather than left to the form's own <c>maxlength</c> attributes.
    /// </summary>
    private const int ShortFieldMaxLength = 200;

    /// <summary>Longest accepted message body, in characters.</summary>
    private const int MessageMaxLength = 5000;

    /// <summary>Handles <see cref="SendContactMessageCommand"/>.</summary>
    /// <param name="cmd">The submitted form.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A task that completes once the relay has accepted the message, and the chat post has been
    /// attempted. Whether that post succeeded is deliberately not visible to the caller.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a required field is missing or a field is longer than this handler accepts.
    /// </exception>
    /// <exception cref="ContactRecipientNotConfiguredException">
    /// Thrown when this deployment configured no enquiry inbox.
    /// </exception>
    /// <exception cref="ContactMessageNotSentException">Thrown when the relay refused the message.</exception>
    public async Task HandleAsync(SendContactMessageCommand cmd, CancellationToken ct)
    {
        var name = Require(cmd.Name, "ContactFieldName", ShortFieldMaxLength);
        var email = Require(cmd.Email, "ContactFieldEmail", ShortFieldMaxLength);
        var message = Require(cmd.Message, "ContactFieldMessage", MessageMaxLength);
        var phone = Optional(cmd.Phone, "ContactFieldPhone", ShortFieldMaxLength);
        var service = Optional(cmd.Service, "ContactFieldService", ShortFieldMaxLength);
        var submittedAt = Optional(cmd.SubmittedAt, "ContactFieldSubmittedAt", ShortFieldMaxLength);

        // The cheapest check that rejects a value MailboxAddress.Parse would throw on inside the
        // sender, where the failure would read as a relay outage rather than as a typo.
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(localizer["ContactEmailInvalid"]);
        }

        var recipient = options.Value.ContactEmail;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            // Error, not Warning: the form is visibly broken for every visitor until this is set,
            // and the setting is named here because nothing else in the process will name it.
            logger.LogError(
                "Contact form submitted but {Setting} is not configured; the message was not delivered.",
                ContactRecipientNotConfiguredException.SettingName);

            throw new ContactRecipientNotConfiguredException();
        }

        var subject = $"Website enquiry from {name}";
        var body = BuildBody(name, email, phone, service, message, submittedAt);

        try
        {
            await emailSender.SendAsync(new EmailMessage(recipient, subject, body), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Logged here rather than only at the middleware, because the relay's own message is
            // the only thing that says whether this is a credential, a certificate or an outage,
            // and it must not travel to the visitor.
            logger.LogError(ex, "Contact form message to {Recipient} was refused by the SMTP relay.", recipient);
            throw new ContactMessageNotSentException(ex);
        }

        // Only after the mail is away, and only ever after: the chat post is the operator's
        // convenience and the mail is the record. Attempting it first would let a Telegram outage
        // hold up a delivery that does not depend on it.
        await NotifyChatAsync(
            new ContactEnquiry(
                name, email, phone, service, message, submittedAt ?? DateTimeOffset.UtcNow.ToString("u")),
            ct);
    }

    /// <summary>
    /// Posts the enquiry to the operator's chat, swallowing any failure of that channel alone.
    /// </summary>
    /// <remarks>
    /// The one <c>catch</c> in this handler that does not rethrow, and the reason is that the mail
    /// above already succeeded. Turning a chat outage into an exception here would answer the
    /// visitor "your message could not be delivered" about a message sitting in the operator's
    /// inbox, and invite them to send it again. <see cref="OperationCanceledException"/> is
    /// excluded because a cancelled request is not a channel failure and must not be logged as
    /// one.
    /// </remarks>
    /// <param name="enquiry">The validated submission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the attempt is over, successful or not.</returns>
    private async Task NotifyChatAsync(ContactEnquiry enquiry, CancellationToken ct)
    {
        try
        {
            await contactNotifier.NotifyAsync(enquiry, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Error, not Warning: a channel an operator believes they are watching has stopped
            // working, and nothing else in the process will say so. The sentence states plainly
            // that the enquiry itself is not lost, so this line cannot be read as a lost message.
            logger.LogError(
                ex,
                "Contact enquiry could not be posted to the operator's chat. It was still delivered to {Recipient} by email.",
                options.Value.ContactEmail);
        }
    }

    /// <summary>
    /// Trims a required field and refuses it when it is empty or over length.
    /// </summary>
    /// <param name="value">The submitted value.</param>
    /// <param name="field">
    /// Resource key of the field's label, looked up in the caller's language and substituted
    /// into the refusal. A key rather than a <c>nameof</c> because the visitor reads this
    /// sentence and "SubmittedAt" is not a word in any of the three languages the portal ships.
    /// </param>
    /// <param name="maxLength">Longest accepted length after trimming, in characters.</param>
    /// <returns>The trimmed value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is missing or too long.</exception>
    private string Require(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidOperationException(localizer["ContactFieldRequired", localizer[field]]);
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new InvalidOperationException(localizer["ContactFieldTooLong", localizer[field], maxLength]);
    }

    /// <summary>
    /// Trims an optional field, mapping blank to <see langword="null"/>, and refuses it when it is
    /// over length.
    /// </summary>
    /// <param name="value">The submitted value, possibly absent.</param>
    /// <param name="field">Resource key of the field's label; see <see cref="Require"/>.</param>
    /// <param name="maxLength">Longest accepted length after trimming, in characters.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when nothing was given.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is too long.</exception>
    private string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new InvalidOperationException(localizer["ContactFieldTooLong", localizer[field], maxLength]);
    }

    /// <summary>
    /// Renders the enquiry as the HTML body of the notification mail.
    /// </summary>
    /// <remarks>
    /// Every visitor-supplied value goes through <see cref="WebUtility.HtmlEncode(string)"/>.
    /// The Nuxt route this replaces interpolated all five straight into its markup, so a message
    /// containing a tag arrived as markup in the operator's mail client.
    /// </remarks>
    /// <param name="name">Sender's name.</param>
    /// <param name="email">Sender's email address.</param>
    /// <param name="phone">Sender's phone number, or <see langword="null"/>.</param>
    /// <param name="service">Service the enquiry is about, or <see langword="null"/>.</param>
    /// <param name="message">The message body.</param>
    /// <param name="submittedAt">The browser's own formatted send time, or <see langword="null"/>.</param>
    /// <returns>The HTML body.</returns>
    private static string BuildBody(
        string name, string email, string? phone, string? service, string message, string? submittedAt)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>New website enquiry</h2>");
        sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" border=\"1\">");
        AppendRow(sb, "Name", name);
        AppendRow(sb, "Email", email);

        if (phone is not null)
        {
            AppendRow(sb, "Phone", phone);
        }

        if (service is not null)
        {
            AppendRow(sb, "Service", service);
        }

        AppendRow(sb, "Submitted", submittedAt ?? DateTimeOffset.UtcNow.ToString("u"));
        sb.Append("</table>");
        sb.Append("<h3>Message</h3>");

        // Newlines become <br> because the body is sent as HTML; without it a paragraphed
        // message arrives as one run-on line.
        sb.Append("<p>").Append(WebUtility.HtmlEncode(message).ReplaceLineEndings("<br>")).Append("</p>");
        return sb.ToString();
    }

    /// <summary>Appends one label/value row to the details table, HTML-encoding both.</summary>
    /// <param name="sb">The body being built.</param>
    /// <param name="label">The row label.</param>
    /// <param name="value">The value, as the visitor typed it.</param>
    private static void AppendRow(StringBuilder sb, string label, string value) =>
        sb.Append("<tr><td><b>").Append(WebUtility.HtmlEncode(label)).Append("</b></td><td>")
          .Append(WebUtility.HtmlEncode(value)).Append("</td></tr>");
}
