using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IEmailSender"/> that accepts every message and first runs a step a test supplies, so the test can do
/// something at the moment a mail is leaving, such as a reader following its unsubscribe link. A fake port standing in for the
/// outbound mail relay, which a test must not call.
/// </summary>
/// <param name="onSend">What happens while the relay is handed a message.</param>
internal sealed class HookedEmailSender(Func<EmailMessage, Task> onSend) : IEmailSender
{
    /// <inheritdoc/>
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        await onSend(message);
        return EmailSendResult.Sent;
    }
}
