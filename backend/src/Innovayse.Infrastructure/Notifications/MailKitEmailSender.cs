namespace Innovayse.Infrastructure.Notifications;

using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Infrastructure.Notifications.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

/// <summary>
/// Sends emails via SMTP using MailKit.
/// Implements <see cref="IEmailSender"/> using the settings from <see cref="SmtpOptions"/>.
/// </summary>
public sealed class MailKitEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    /// <summary>Resolved SMTP configuration.</summary>
    private readonly SmtpOptions _settings = options.Value;

    /// <summary>
    /// Sends an email message via the configured SMTP server.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message is accepted by the SMTP server.</returns>
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.From.Name, _settings.From.Address));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        var textPart = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };
        textPart.ContentTransferEncoding = MimeKit.ContentEncoding.Base64;
        mime.Body = textPart;

        using var client = new SmtpClient();

        // SecureSocketOptions rather than the bool overload: that overload maps false to
        // "no TLS at all", which cannot express the difference between the three wire
        // protocols here. 465 is implicit TLS — the session is encrypted before the first
        // command — while 587 negotiates it with STARTTLS after a plaintext greeting, and a
        // catcher on 1025 speaks neither.
        //
        // The deployment states which; the port no longer decides. It used to, with UseSsl
        // as an override, and that was one value short of the question: `false` could not be
        // told apart from "nobody set this", so a relay on a non-standard port that wants TLS
        // from the first byte looked exactly like a local catcher — and guessing that one
        // wrong puts the relay's password on the wire in the clear. The override existed
        // precisely because the guess was wrong somewhere; SmtpEncryption.Ssl is that
        // override, now spelled as the answer rather than as a correction to one.
        //
        // Encryption is checked at startup (see AddInfrastructure), so null cannot reach here
        // from a running host. It still refuses rather than falling through to None: an
        // instance built without configuration must not quietly become the unencrypted case.
        var security = _settings.Encryption switch
        {
            SmtpEncryption.Ssl => SecureSocketOptions.SslOnConnect,
            SmtpEncryption.Tls => SecureSocketOptions.StartTls,
            SmtpEncryption.None => SecureSocketOptions.None,
            _ => throw new InvalidOperationException(
                $"{SmtpOptions.SectionName}:{nameof(SmtpOptions.Encryption)} is not set. It must "
                    + "be one of tls (STARTTLS), ssl (implicit TLS) or none (a local catcher)."),
        };
        await client.ConnectAsync(_settings.Host, _settings.Port, security, ct);

        // Only when a username is configured. A local catcher accepts no credentials at all
        // and refuses the AUTH command, and authenticating with a blank username against a
        // real relay fails the send outright — which is what a mis-spelled configuration key
        // produced here, silently, for every password reset and address verification.
        if (!string.IsNullOrWhiteSpace(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
