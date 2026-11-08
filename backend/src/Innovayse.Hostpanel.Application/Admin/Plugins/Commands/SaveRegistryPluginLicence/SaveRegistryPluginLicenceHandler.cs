using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SaveRegistryPluginLicence;

/// <summary>
/// Handles <see cref="SaveRegistryPluginLicenceCommand"/>: keeps the key as an encrypted secret setting, the way the
/// secret fields of an integration are kept, and audits the change without the key.
/// </summary>
public sealed class SaveRegistryPluginLicenceHandler
{
    /// <summary>Reads and writes the setting row.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Encrypts the key before it is stored.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Stages the activity log line.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>The caller, for the secret setting audit line.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Writes the secret setting audit line.</summary>
    private readonly ILogger<SaveRegistryPluginLicenceHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="db">Reads and writes the setting row.</param>
    /// <param name="cipher">Encrypts the key before it is stored.</param>
    /// <param name="activityLogger">Stages the activity log line.</param>
    /// <param name="currentUser">The caller, for the secret setting audit line.</param>
    /// <param name="logger">Writes the secret setting audit line.</param>
    public SaveRegistryPluginLicenceHandler(
        IHostpanelDbContext db,
        ISecretSettingCipher cipher,
        IActivityLogger activityLogger,
        ICurrentUserService currentUser,
        ILogger<SaveRegistryPluginLicenceHandler> logger)
    {
        _db = db;
        _cipher = cipher;
        _activityLogger = activityLogger;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Stores the licence key of a plugin.</summary>
    /// <param name="command">The plugin id and the key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success; this use case refuses nothing the validator has not already refused.</returns>
    public async Task<Result> HandleAsync(SaveRegistryPluginLicenceCommand command, CancellationToken ct)
    {
        var key = PluginSettingKeys.RegistryLicenceKey(command.PluginId);
        var protectedKey = _cipher.Protect(command.Key.Trim());

        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            _db.Settings.Add(Setting.Create(key, protectedKey, $"Registry licence key of the {command.PluginId} plugin."));
        }
        else
        {
            existing.UpdateValue(protectedKey);
        }

        // The audit line names the plugin and the kind of change; the key never reaches it.
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, $"Saved Plugin Licence Key - Plugin ID: {command.PluginId}"), ct);
        await _db.SaveChangesAsync(ct);
        SecretSettingAudit.Record(_logger, key, SecretSettingChange.Replaced, _currentUser.UserId);

        return Result.Success();
    }
}
