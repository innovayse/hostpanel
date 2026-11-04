using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/*
 * Why a removal switches the plugin off, and why it keeps the plugin's data.
 *
 * A removed plugin stays loaded in this process until the restart its removal asks for. PluginActivator.PrepareAsync
 * reads the plugin's integration:{id}:is_enabled row from the database on every call (no cache) and lets the plugin
 * run only when that row equals "true", ignoring case; IntegrationSettingKeys.DisabledValue ("false") fails that
 * gate. So once this handler's save commits, every route, slot and resolver refuses the still-loaded plugin.
 *
 * The data the plugin kept (PluginDataEntries and their scopes) is NOT deleted here: a removal is how a reinstall
 * starts, and the plugin's state must survive it. The operator deletes it deliberately with PurgePluginDataCommand,
 * which is audited.
 *
 * A package that is already gone (PLUGIN_NOT_FOUND from the store) is still switched off: that is what an
 * administrator retries after a removal whose save failed once the folder was deleted, and what clears a plugin whose
 * folder was removed by hand. Only a valid, non-built-in id reaches that path, because the store refuses an invalid id
 * and a built-in plugin first, so a refused removal never touches the switch.
 */

/// <summary>
/// Handles <see cref="RemovePluginCommand"/>: asks the plugin package store to delete an uploaded plugin (and any
/// upgrade staged for it), then in one save switches the plugin off and records the removal in the activity log. The
/// data the plugin kept stays until an operator purges it.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>Deletes the uploaded plugin's folder, and refuses an invalid id or a built-in plugin.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the removal on the same context as the switch.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>The Admin module's data context: loads and saves the plugin's switch row with the audit line.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Drops the panels' remembered plugin availability once the switch-off has committed.</summary>
    private readonly IPluginAvailabilityInvalidator _availability;

    /// <summary>The clock the moment of the removal is read from, for the removal event.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Deletes the uploaded plugin's folder, and refuses an invalid id or a built-in plugin.</param>
    /// <param name="activityLogger">Stages the audit line for the removal on the same context as the switch.</param>
    /// <param name="context">The Admin module's data context: loads and saves the plugin's switch row with the audit line.</param>
    /// <param name="availability">Drops the panels' remembered plugin availability once the switch-off has committed.</param>
    /// <param name="clock">The clock the moment of the removal is read from, for the removal event.</param>
    public RemovePluginHandler(
        IPluginPackageStore store,
        IActivityLogger activityLogger,
        IHostpanelDbContext context,
        IPluginAvailabilityInvalidator availability,
        IClock clock)
    {
        _store = store;
        _activityLogger = activityLogger;
        _context = context;
        _availability = availability;
        _clock = clock;
    }

    /// <summary>
    /// Deletes the uploaded plugin with the given id when its package is still there, then switches the plugin off and
    /// records who removed it, in one save. The plugin's stored data is kept.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with <c>RequiresRestart</c> <see langword="true"/> when the package was removed (the plugin stays
    /// loaded, switched off, until the restart); success with <c>RequiresRestart</c> <see langword="false"/> when the
    /// package was already gone and only the switch was turned off; <c>PLUGIN_NOT_FOUND</c> when the
    /// package is gone and the plugin was never switched on here (no switch row), so nothing exists to clear;
    /// <c>PLUGIN_ID_INVALID</c> when
    /// the id breaks the plugin id rule; <c>PLUGIN_BUILT_IN</c> when the plugin ships with the build; otherwise the
    /// code of any other refusal the package store answers.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(RemovePluginCommand command, CancellationToken ct)
    {
        var outcome = await _store.RemoveAsync(new PluginPackageRemoval(command.Id), ct);
        if (outcome.Outcome is not (PluginPackageOutcome.Removed or PluginPackageOutcome.NotFound))
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        var packageRemoved = outcome.Outcome == PluginPackageOutcome.Removed;
        var switchKey = IntegrationSettingKeys.EnabledKey(command.Id);

        // Loaded tracked, so the switch-off is an update of the row the activator reads, not a second row.
        var pluginSwitch = await _context.Settings.FirstOrDefaultAsync(s => s.Key == switchKey, ct);

        // No package and no switch row: the plugin was never switched on here, so it can have run no call and kept no
        // data, and there is nothing to clear. Answering not found keeps a mistyped id from leaving a settings row behind.
        if (!packageRemoved && pluginSwitch is null)
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        if (pluginSwitch is null)
        {
            _context.Settings.Add(Setting.CreateSwitchedOffForRemovedPlugin(command.Id, _clock.UtcNow));
        }
        else
        {
            pluginSwitch.SwitchOffRemovedPlugin(command.Id, _clock.UtcNow);
        }

        var description = packageRemoved
            ? $"Removed Plugin - Plugin ID: {command.Id}"
            : $"Switched Off Removed Plugin - Plugin ID: {command.Id} - Package already gone";
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, description), ct);

        // One save: the switch and the audit line commit together.
        await _context.SaveChangesAsync(ct);

        // After the commit, never before: a lookup between an earlier drop and the commit would remember the plugin as
        // still switched on, and the panels would keep showing its pages for the cache lifetime.
        _availability.Invalidate();

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: packageRemoved));
    }
}
