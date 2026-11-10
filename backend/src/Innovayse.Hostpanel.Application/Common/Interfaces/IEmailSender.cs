using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Abstraction over email sending providers.</summary>
public interface IEmailSender
{
    /// <summary>Sends an email message.</summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="EmailSendResult.Sent"/> once the relay accepted the message, otherwise the
    /// classified reason it did not. A refusal is answered, never thrown; the adapter logs the
    /// relay's own wording, which no caller stores or shows.
    /// </returns>
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}
