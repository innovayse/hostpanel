using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.ClearRegistryPluginLicence;

/// <summary>Handles <see cref="ClearRegistryPluginLicenceCommand"/>: deletes the stored key row and audits it without the key.</summary>
public sealed class ClearRegistryPluginLicenceHandler
{
    /// <summary>Reads and deletes the setting row.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Stages the activity log line.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>The caller, for the secret setting audit line.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Writes the secret setting audit line.</summary>
    private readonly ILogger<ClearRegistryPluginLicenceHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="db">Reads and deletes the setting row.</param>
    /// <param name="activityLogger">Stages the activity log line.</param>
    /// <param name="currentUser">The caller, for the secret setting audit line.</param>
    /// <param name="logger">Writes the secret setting audit line.</param>
    public ClearRegistryPluginLicenceHandler(
        IHostpanelDbContext db,
        IActivityLogger activityLogger,
        ICurrentUserService currentUser,
        ILogger<ClearRegistryPluginLicenceHandler> logger)
    {
        _db = db;
        _activityLogger = activityLogger;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Deletes the licence key of a plugin.</summary>
    /// <param name="command">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, whether or not a key was stored.</returns>
    public async Task<Result> HandleAsync(ClearRegistryPluginLicenceCommand command, CancellationToken ct)
    {
        var key = PluginSettingKeys.RegistryLicenceKey(command.PluginId);
        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            return Result.Success();
        }

        _db.Settings.Remove(existing);
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, $"Cleared Plugin Licence Key - Plugin ID: {command.PluginId}"), ct);
        await _db.SaveChangesAsync(ct);
        SecretSettingAudit.Record(_logger, key, SecretSettingChange.Cleared, _currentUser.UserId);

        return Result.Success();
    }
}
