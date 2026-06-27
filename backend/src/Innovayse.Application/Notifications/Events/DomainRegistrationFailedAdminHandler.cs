namespace Innovayse.Application.Notifications.Events;

using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Notifications.Settings;
using Innovayse.Domain.Domains.Events;
using Microsoft.Extensions.Options;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainRegistrationFailedEvent"/> and alerts the platform admin
/// by email so the team is aware of failed registrations that required automatic refunds.
/// </summary>
public sealed class DomainRegistrationFailedAdminHandler(
    IMessageBus bus,
    IOptions<NotificationSettings> settings)
{
    /// <summary>
    /// Dispatches a domain-registration-failed-admin notification email to the configured admin address.
    /// </summary>
    /// <param name="evt">The domain registration failed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainRegistrationFailedEvent evt, CancellationToken ct)
    {
        var data = new
        {
            domain = new { name = evt.DomainName },
            client = new { id = evt.ClientId },
            invoice = new { id = evt.InvoiceId },
            reason = evt.Reason,
        };

        await bus.InvokeAsync(
            new SendEmailCommand(settings.Value.AdminEmail, "domain-registration-failed-admin", data), ct);
    }
}
