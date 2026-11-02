using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="DomainRegistrationFailedEvent"/> and alerts the platform admin
/// by email so the team is aware of failed registrations that required automatic refunds.
/// </summary>
public sealed class DomainRegistrationFailedAdminHandler
{
    /// <summary>The Wolverine bus this handler dispatches the notification email through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The configured staff addresses the alert is sent to (admin, falling back to contact).</summary>
    private readonly IOptions<NotificationOptions> _settings;

    /// <summary>Records an alert that could not be mailed because no staff address is configured.</summary>
    private readonly ILogger<DomainRegistrationFailedAdminHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus this handler dispatches the notification email through.</param>
    /// <param name="settings">The configured staff addresses (admin, falling back to contact).</param>
    /// <param name="logger">Records an alert that could not be mailed because no staff address is configured.</param>
    public DomainRegistrationFailedAdminHandler(
        IMessageBus bus,
        IOptions<NotificationOptions> settings,
        ILogger<DomainRegistrationFailedAdminHandler> logger)
    {
        _bus = bus;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a domain-registration-failed-admin notification email to the staff address, or logs
    /// the alert when none is configured.
    /// </summary>
    /// <param name="evt">The domain registration failed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainRegistrationFailedEvent evt, CancellationToken ct)
    {
        // An unset address used to reach SendEmail's validator and throw. This event's client handler
        // shares the chain, so the retry policy then replayed it: the client mail was repeated on every
        // attempt or, run after this one, never sent. Log and return like the other staff alerts.
        var to = StaffAlertAddress.Resolve(_settings.Value);
        if (to is null)
        {
            _logger.LogError(
                "Domain registration alert for {DomainName} (invoice {InvoiceId}) not mailed: set {Setting} (or Notifications:ContactEmail). Reason: {Reason}",
                evt.DomainName, evt.InvoiceId, "Notifications:AdminEmail", evt.Reason);
            return;
        }

        var data = new DomainRegistrationFailedAdminTemplateModel(
            new DomainNameTemplateData(evt.DomainName),
            new ClientRefTemplateData(evt.ClientId),
            new InvoiceRefTemplateData(evt.InvoiceId),
            evt.Reason);

        await _bus.InvokeAsync(
            new SendEmailCommand(
                to,
                null,
                "domain-registration-failed-admin",
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "domain-registration-failed-admin")),
            ct);
    }
}
