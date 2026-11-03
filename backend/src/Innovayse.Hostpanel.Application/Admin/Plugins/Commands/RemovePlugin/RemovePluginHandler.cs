using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/*
 * Why a removal switches the plugin off and announces it, in one save, instead of purging inline.
 *
 * A removed plugin stays loaded in this process until the restart its removal asks for. PluginActivator.PrepareAsync
 * reads the plugin's integration:{id}:is_enabled row from the database on every call (no cache) and lets the plugin
 * run only when that row equals "true", ignoring case; IntegrationSettingKeys.DisabledValue ("false") fails that
 * gate. So once this handler's save commits, every route, slot and resolver refuses the still-loaded plugin, and it
 * can no longer write store rows behind the purge.
 *
 * The same save stages the activity line and, through Setting.SwitchOffRemovedPlugin, raises PluginRemovedEvent,
 * which the domain-event interceptor writes to Wolverine's outbox in that transaction. The purge is the Plugins
 * module's durable, retried handler of that event (PurgeRemovedPluginDataHandler): it runs if and only if the switch
 * committed, and a crash after the commit is recovered from the stored envelope. The old inline purge after a
 * separate save could leave the data forever.
 *
 * A package that is already gone (PLUGIN_NOT_FOUND from the store) is still switched off and purged: that is what an
 * administrator retries after a removal whose save failed once the folder was deleted, and what clears a plugin whose
 * folder was removed by hand. Only a valid, non-built-in id reaches that path, because the store refuses an invalid id
 * and a built-in plugin first, so a refused removal never touches the switch or the data.
 */

/// <summary>
/// Handles <see cref="RemovePluginCommand"/>: asks the plugin package store to delete an uploaded plugin, then in one
/// save switches the plugin off, records the removal in the activity log and announces it with
/// <see cref="PluginRemovedEvent"/>, whose handler purges the data the plugin kept.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>Deletes the uploaded plugin's folder, and refuses an invalid id or a built-in plugin.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the removal on the same context as the switch.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>The Admin module's data context: loads and saves the plugin's switch row with the audit line.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The moment the removal is stamped with on its event.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Deletes the uploaded plugin's folder, and refuses an invalid id or a built-in plugin.</param>
    /// <param name="activityLogger">Stages the audit line for the removal on the same context as the switch.</param>
    /// <param name="context">The Admin module's data context: loads and saves the plugin's switch row with the audit line.</param>
    /// <param name="clock">The moment the removal is stamped with on its event.</param>
    public RemovePluginHandler(
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

    /// <summary>
    /// Deletes the uploaded plugin with the given id when its package is still there, then switches the plugin off,
    /// records who removed it and announces the removal so its stored data is purged, all in one save.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with <c>RequiresRestart</c> <see langword="true"/> when the package was removed (the plugin stays
    /// loaded, switched off, until the restart); success with <c>RequiresRestart</c> <see langword="false"/> when the
    /// package was already gone and only the switch and the stored data were cleared; <c>PLUGIN_NOT_FOUND</c> when the
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
        var removedAt = _clock.UtcNow;
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
            _context.Settings.Add(Setting.CreateSwitchedOffForRemovedPlugin(command.Id, removedAt));
        }
        else
        {
            pluginSwitch.SwitchOffRemovedPlugin(command.Id, removedAt);
        }

        var description = packageRemoved
            ? $"Removed Plugin - Plugin ID: {command.Id}"
            : $"Cleared Removed Plugin - Plugin ID: {command.Id} - Package already gone";
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, description), ct);

        // One save: the switch, the audit line and the outbox envelope of PluginRemovedEvent commit together.
        await _context.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: packageRemoved));
    }
}
