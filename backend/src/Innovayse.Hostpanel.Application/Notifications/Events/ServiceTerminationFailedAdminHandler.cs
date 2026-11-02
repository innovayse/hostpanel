using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Services.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="ServiceTerminationFailedEvent"/> by mailing staff: an automatic termination
/// could not remove the hosting account, so the service waits for the next run.
/// </summary>
public sealed class ServiceTerminationFailedAdminHandler
{
    /// <summary>Sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where staff mail goes.</summary>
    private readonly IOptions<NotificationOptions> _settings;

    /// <summary>For seeding the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Names the missing setting when no staff address is configured.</summary>
    private readonly ILogger<ServiceTerminationFailedAdminHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Sends the mail.</param>
    /// <param name="settings">Where staff mail goes.</param>
    /// <param name="context">For seeding the template.</param>
    /// <param name="logger">Names the missing setting when no staff address is configured.</param>
    public ServiceTerminationFailedAdminHandler(
        IMessageBus bus,
        IOptions<NotificationOptions> settings,
        IHostpanelDbContext context,
        ILogger<ServiceTerminationFailedAdminHandler> logger)
    {
        _bus = bus;
        _settings = settings;
        _context = context;
        _logger = logger;
    }

    /// <summary>Mails staff.</summary>
    /// <param name="evt">The failure.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task HandleAsync(ServiceTerminationFailedEvent evt, CancellationToken ct)
    {
        var to = StaffAlertAddress.Resolve(_settings.Value);
        if (to is null)
        {
            _logger.LogError(
                "Termination alert for service {ServiceId} not mailed: set {Setting} (or Notifications:ContactEmail). Reason: {Reason}",
                evt.ServiceId, "Notifications:AdminEmail", evt.Reason);
            return;
        }

        await StaffAlertTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new ServiceTerminationFailedAdminTemplateModel(new ServiceRefTemplateData(evt.ServiceId), evt.Reason);
        await _bus.InvokeAsync(
            new SendEmailCommand(
                to,
                null,
                StaffAlertTemplatesSeeder.ServiceTerminationFailedSlug,
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(
                    evt,
                    StaffAlertTemplatesSeeder.ServiceTerminationFailedSlug)),
            ct);
    }
}
