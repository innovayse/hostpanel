using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Commands.PurgePluginData;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Handles <see cref="RemovePluginCommand"/>: asks the plugin store to delete an uploaded plugin
/// records the removal in the activity log and then has the plugin store module delete the data the
/// plugin kept.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>Deletes the uploaded plugin's folder.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the removal.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Asks the module that owns the plugin store to delete the removed plugin's data.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Deletes the uploaded plugin's folder.</param>
    /// <param name="activityLogger">Stages the audit line for the removal.</param>
    /// <param name="db">Saves the audit line.</param>
    /// <param name="bus">Asks the module that owns the plugin store to delete the removed plugin's data.</param>
    public RemovePluginHandler(
        IPluginPackageStore store,
        IActivityLogger activityLogger,
        IHostpanelDbContext db,
        IMessageBus bus)
    {
        _store = store;
        _activityLogger = activityLogger;
        _db = db;
        _bus = bus;
    }

    /// <summary>
    /// Deletes the uploaded plugin with the given id, records who removed it and purges the data it kept
    /// in the plugin store.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A result requiring a restart when the plugin and its stored data were removed;
    /// <c>PLUGIN_ID_INVALID</c> when the id breaks the plugin id rule; <c>PLUGIN_BUILT_IN</c> when the
    /// plugin ships with the build; <c>PLUGIN_NOT_FOUND</c> when no uploaded plugin with that id exists;
    /// otherwise the failure code of the data purge.
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

        // Purge only after a successful removal, so a refused removal (built-in, not found) never deletes
        // data; the module that owns PluginDataEntries does the delete, not this handler.
        var purge = await _bus.InvokeAsync<Result>(new PurgePluginDataCommand(command.Id), ct);
        if (purge.IsFailure)
        {
            return Result<PluginActionResultDto>.FailureCode(
                purge.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: true));
    }
}
