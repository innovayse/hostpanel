using System.Net.Sockets;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Infrastructure.Email.Enums;
using Innovayse.Hostpanel.Infrastructure.Email.Settings;
using Innovayse.Hostpanel.Shared.Infrastructure;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Innovayse.Hostpanel.Infrastructure.Email;

/// <summary>
/// Sends emails via SMTP using MailKit.
/// Implements <see cref="IEmailSender"/> using the settings from <see cref="SmtpOptions"/>.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    /// <summary>The value of <c>List-Unsubscribe-Post</c> that RFC 8058 fixes: the mail client posts this body to the unsubscribe address.</summary>
    private const string OneClickPostValue = "List-Unsubscribe=One-Click";

    /// <summary>Resolved SMTP configuration.</summary>
    private readonly SmtpOptions _settings;

    /// <summary>Records the relay's own wording of a refusal, which no caller stores or shows.</summary>
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>Initializes a new instance of the <see cref="SmtpEmailSender"/> class.</summary>
    /// <param name="options">The relay's host, port, encryption, credentials and sender address.</param>
    /// <param name="logger">Records the relay's own wording of a refusal.</param>
    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends an email message via the configured SMTP server.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="EmailSendResult.Sent"/> once the server accepted the message, otherwise the classified reason it did not.</returns>
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            await DeliverAsync(message, ct);
            return EmailSendResult.Sent;
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The relay's text can name servers, addresses and credential hints, so it goes to the
            // log only; callers get the closed reason, which is all they store or show.
            var failure = Classify(ex);
            _logger.LogError(ex, "The SMTP relay did not accept a message: {Failure}.", failure);
            return EmailSendResult.Failed(failure);
        }
    }

    /// <summary>
    /// Builds the MIME message. A marketing message (one that carries an unsubscribe address) is marked with <c>List-Unsubscribe</c>
    /// (RFC 2369) and <c>List-Unsubscribe-Post</c> (RFC 8058), which lets a mail client show its own unsubscribe button and post
    /// to the address without opening a page. The address is therefore an endpoint that unsubscribes on a bare POST.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <returns>The message as it goes to the relay.</returns>
    public MimeMessage Compose(EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.From.Name, _settings.From.Address));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        var textPart = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };
        textPart.ContentTransferEncoding = MimeKit.ContentEncoding.Base64;
        mime.Body = textPart;

        if (message.UnsubscribeUrl is { } unsubscribeUrl)
        {
            mime.Headers.Add(HeaderId.ListUnsubscribe, $"<{unsubscribeUrl}>");
            mime.Headers.Add(HeaderId.ListUnsubscribePost, OneClickPostValue);
        }

        return mime;
    }

    /// <summary>Maps what the mail library threw to the closed set of reasons callers understand.</summary>
    /// <param name="ex">The exception the send ended with.</param>
    /// <returns>The reason.</returns>
    private static EmailSendFailure Classify(Exception ex) => ex switch
    {
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } => EmailSendFailure.RecipientRejected,
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } => EmailSendFailure.SenderRejected,
        SmtpCommandException { ErrorCode: SmtpErrorCode.MessageNotAccepted } => EmailSendFailure.MessageRejected,
        AuthenticationException => EmailSendFailure.AuthenticationRejected,
        SslHandshakeException => EmailSendFailure.SecureConnectionFailed,
        ParseException => EmailSendFailure.RecipientRejected,
        TimeoutException or OperationCanceledException => EmailSendFailure.Timeout,
        SocketException or IOException or ServiceNotConnectedException => EmailSendFailure.RelayUnreachable,
        _ => EmailSendFailure.Unknown,
    };

    /// <summary>Builds the MIME message and hands it to the configured SMTP server.</summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the server accepted the message; it throws otherwise.</returns>
    private async Task DeliverAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = Compose(message);

        using var client = new SmtpClient();

        // SecureSocketOptions rather than the bool overload: that overload maps false to
        // "no TLS at all", which cannot express the difference between the three wire
        // protocols here. 465 is implicit TLS, the session is encrypted before the first
        // command, while 587 negotiates it with STARTTLS after a plaintext greeting, and a
        // catcher on 1025 speaks neither.
        //
        // The deployment states which; the port does not decide. A relay on a non-standard port
        // that wants TLS from the first byte must not be mistaken for a local catcher, because
        // guessing that one wrong puts the relay's password on the wire in the clear.
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
        // real relay fails the send outright, which is what a mis-spelled configuration key
        // produced here, silently, for every password reset and address verification.
        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
