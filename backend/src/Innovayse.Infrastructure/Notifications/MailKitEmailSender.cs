namespace Innovayse.Infrastructure.Notifications;

using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

/// <summary>
/// Sends emails via SMTP using MailKit.
/// Implements <see cref="IEmailSender"/> using the settings from <see cref="SmtpSettings"/>.
/// </summary>
public sealed class MailKitEmailSender(IOptions<SmtpSettings> options) : IEmailSender
{
    /// <summary>Resolved SMTP configuration.</summary>
    private readonly SmtpSettings _settings = options.Value;

    /// <summary>
    /// Sends an email message via the configured SMTP server.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message is accepted by the SMTP server.</returns>
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        var textPart = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };
        textPart.ContentTransferEncoding = MimeKit.ContentEncoding.Base64;
        mime.Body = textPart;

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
