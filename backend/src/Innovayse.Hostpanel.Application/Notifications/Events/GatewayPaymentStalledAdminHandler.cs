using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="GatewayPaymentStalledEvent"/> by mailing staff: a hosted gateway payment is
/// still pending after an unusual time, so its invoice stays unresolved.
/// </summary>
public sealed class GatewayPaymentStalledAdminHandler
{
    /// <summary>Sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where staff mail goes.</summary>
    private readonly IOptions<NotificationOptions> _settings;

    /// <summary>For seeding the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Names the missing setting when no staff address is configured.</summary>
    private readonly ILogger<GatewayPaymentStalledAdminHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Sends the mail.</param>
    /// <param name="settings">Where staff mail goes.</param>
    /// <param name="context">For seeding the template.</param>
    /// <param name="logger">Names the missing setting when no staff address is configured.</param>
    public GatewayPaymentStalledAdminHandler(
        IMessageBus bus,
        IOptions<NotificationOptions> settings,
        IHostpanelDbContext context,
        ILogger<GatewayPaymentStalledAdminHandler> logger)
    {
        _bus = bus;
        _settings = settings;
        _context = context;
        _logger = logger;
    }

    /// <summary>Mails staff.</summary>
    /// <param name="evt">The stalled payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task HandleAsync(GatewayPaymentStalledEvent evt, CancellationToken ct)
    {
        var to = StaffAlertAddress.Resolve(_settings.Value);
        if (to is null)
        {
            _logger.LogError(
                "Pending payment alert for invoice {InvoiceId} not mailed: set {Setting} (or Notifications:ContactEmail).",
                evt.InvoiceId, "Notifications:AdminEmail");
            return;
        }

        await StaffAlertTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new GatewayPaymentStalledAdminTemplateModel(
            new InvoiceRefTemplateData(evt.InvoiceId),
            new ClientRefTemplateData(evt.ClientId),
            evt.Module,
            evt.PendingHours);
        await _bus.InvokeAsync(
            new SendEmailCommand(
                to,
                null,
                StaffAlertTemplatesSeeder.GatewayPaymentStalledSlug,
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(
                    evt,
                    StaffAlertTemplatesSeeder.GatewayPaymentStalledSlug)),
            ct);
    }
}
