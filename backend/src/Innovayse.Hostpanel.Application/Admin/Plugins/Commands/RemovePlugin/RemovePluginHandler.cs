using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Handles <see cref="RemovePluginCommand"/>: asks the plugin store to delete an uploaded plugin
/// and records the removal in the activity log.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>Deletes the uploaded plugin's folder.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the removal.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Deletes the uploaded plugin's folder.</param>
    /// <param name="activityLogger">Stages the audit line for the removal.</param>
    /// <param name="db">Saves the audit line.</param>
    public RemovePluginHandler(IPluginPackageStore store, IActivityLogger activityLogger, IHostpanelDbContext db)
    {
        _store = store;
        _activityLogger = activityLogger;
        _db = db;
    }

    /// <summary>
    /// Deletes the uploaded plugin with the given id and records who removed it.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A result requiring a restart when the plugin was removed; <c>PLUGIN_ID_INVALID</c> when the
    /// id breaks the plugin id rule; <c>PLUGIN_BUILT_IN</c> when the plugin ships with the build;
    /// <c>PLUGIN_NOT_FOUND</c> when no uploaded plugin with that id exists.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(RemovePluginCommand command, CancellationToken ct)
    {
        var outcome = await _store.RemoveAsync(new PluginPackageRemoval(command.Id), ct);
        if (outcome.Outcome != PluginPackageOutcome.Removed)
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        await _activityLogger.RecordAsync(
            new ActivityLogEntry(null, $"Removed Plugin - Plugin ID: {outcome.PluginId}"), ct);
        await _db.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: true));
    }
}
