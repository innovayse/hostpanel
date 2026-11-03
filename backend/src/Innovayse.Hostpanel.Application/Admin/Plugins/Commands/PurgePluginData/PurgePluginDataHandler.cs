using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.PurgePluginData;

/*
 * Why the purge is an event, and what hooks into it.
 *
 * The handler only decides that a purge is allowed and records it; the deletion is the durable, retried handlers of
 * PluginDataPurgeRequestedEvent, which the plugin's switch row raises (Setting.RequestDataPurge) in the same save that
 * stores the audit line. The event is the one step every kind of data a plugin leaves in the host shares:
 * PurgeRemovedPluginDataHandler deletes the plugin store, and any other module that keeps rows per plugin id (the slot
 * widget product bindings) adds its own handler of the same event and deletes only what was written up to the
 * event's OccurredAt, so a row the plugin wrote after a reinstall is kept.
 */

/// <summary>
/// Handles <see cref="PurgePluginDataCommand"/>: refuses while the plugin's package is installed, otherwise records
/// the operator's purge in the activity log and raises <see cref="PluginDataPurgeRequestedEvent"/> in one save.
/// </summary>
public sealed class PurgePluginDataHandler
{
    /// <summary>Tells whether the plugin's package folder is still on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the purge on the same context as the event.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Loads the plugin's switch row, reads whether data exists and saves the audit line with the event.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The moment the purge is stamped with on its event.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Tells whether the plugin's package is still on disk.</param>
    /// <param name="activityLogger">Stages the audit line for the purge on the same context as the event.</param>
    /// <param name="context">Loads the plugin's switch row, reads whether data exists and saves the audit line with the event.</param>
    /// <param name="clock">The moment the purge is stamped with on its event.</param>
    public PurgePluginDataHandler(
        IPluginPackageStore store,
        IActivityLogger activityLogger,
        IHostpanelDbContext context,
        IClock clock)
    {
        _store = store;
        _activityLogger = activityLogger;
        _context = context;
        _clock = clock;
    }

    /// <summary>Starts the purge of a removed plugin's kept data and records who asked.</summary>
    /// <param name="command">Command naming the plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (no restart needed) once the purge is recorded and queued; <c>PLUGIN_STILL_INSTALLED</c> when the
    /// plugin's package is on disk, built-in ones included; <c>PLUGIN_NOT_FOUND</c> when the plugin keeps no data.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(PurgePluginDataCommand command, CancellationToken ct)
    {
        // The folder is looked up by the validated id itself, not through the list of readable manifests: a package whose
        // plugin.json is corrupt is not in that list, yet it is on disk and would load again once the file is fixed.
        if (_store.HasPackageFolder(command.Id))
        {
            return Result<PluginActionResultDto>.FailureCode(HostpanelErrorCodes.PLUGIN_STILL_INSTALLED);
        }

        // Every table that keeps rows per plugin id counts: a plugin whose only leftover is its slot widget bindings is
        // still a removed plugin with data to purge.
        var keepsData = await _context.PluginDataScopes.AsNoTracking().AnyAsync(s => s.PluginId == command.Id, ct)
            || await _context.PluginSlotProductBindings.AsNoTracking().AnyAsync(b => b.PluginId == command.Id, ct);
        if (!keepsData)
        {
            return Result<PluginActionResultDto>.FailureCode(HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        }

        var switchKey = IntegrationSettingKeys.EnabledKey(command.Id);
        var pluginSwitch = await _context.Settings.FirstOrDefaultAsync(s => s.Key == switchKey, ct);
        if (pluginSwitch is null)
        {
            pluginSwitch = Setting.CreateSwitchedOffForRemovedPlugin(command.Id);
            _context.Settings.Add(pluginSwitch);
        }
        else
        {
            // A package deleted by hand can leave the switch on while the plugin is still loaded: turn it off so it
            // cannot write behind the purge.
            pluginSwitch.SwitchOffRemovedPlugin(command.Id);
        }

        pluginSwitch.RequestDataPurge(command.Id, _clock.UtcNow);
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(null, $"Purged Plugin Data - Plugin ID: {command.Id}"), ct);

        // One save: the switch, the audit line and the outbox envelope of PluginDataPurgeRequestedEvent commit together.
        await _context.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: false));
    }
}
