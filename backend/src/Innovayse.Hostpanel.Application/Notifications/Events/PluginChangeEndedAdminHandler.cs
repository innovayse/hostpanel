using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="PluginChangeEndedEvent"/> by mailing staff: a plugin change the host applied in the background has
/// ended, either with the new version loaded and running or with the old one restored (or the plugin left uninstalled).
/// </summary>
public sealed class PluginChangeEndedAdminHandler
{
    /// <summary>Sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Where staff mail goes.</summary>
    private readonly IOptions<NotificationOptions> _settings;

    /// <summary>For seeding the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Names the missing setting when no staff address is configured.</summary>
    private readonly ILogger<PluginChangeEndedAdminHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Sends the mail.</param>
    /// <param name="settings">Where staff mail goes.</param>
    /// <param name="context">For seeding the template.</param>
    /// <param name="logger">Names the missing setting when no staff address is configured.</param>
    public PluginChangeEndedAdminHandler(
        IMessageBus bus,
        IOptions<NotificationOptions> settings,
        IHostpanelDbContext context,
        ILogger<PluginChangeEndedAdminHandler> logger)
    {
        _bus = bus;
        _settings = settings;
        _context = context;
        _logger = logger;
    }

    /// <summary>Mails staff.</summary>
    /// <param name="evt">The change that ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task HandleAsync(PluginChangeEndedEvent evt, CancellationToken ct)
    {
        var to = StaffAlertAddress.Resolve(_settings.Value);
        if (to is null)
        {
            _logger.LogError(
                "Plugin change alert for plugin {PluginId} not mailed: set {Setting} (or Notifications:ContactEmail).",
                evt.PluginId, "Notifications:AdminEmail");
            return;
        }

        await StaffAlertTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new PluginChangeEndedAdminTemplateModel(
            evt.PluginId, evt.FromVersion, evt.ToVersion, evt.Applied, evt.Error, evt.RestoredVersion);
        await _bus.InvokeAsync(
            new SendEmailCommand(
                to,
                null,
                StaffAlertTemplatesSeeder.PluginChangeEndedSlug,
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(
                    evt,
                    StaffAlertTemplatesSeeder.PluginChangeEndedSlug)),
            ct);
    }
}
