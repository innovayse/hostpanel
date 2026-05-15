namespace Innovayse.Domain.Notifications.Interfaces;

/// <summary>Abstraction over email sending providers.</summary>
public interface IEmailSender
{
    /// <summary>Sends an email message.</summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been sent.</returns>
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
