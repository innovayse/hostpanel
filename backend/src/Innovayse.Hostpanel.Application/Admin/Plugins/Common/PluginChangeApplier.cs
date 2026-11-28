using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Applies an accepted plugin change right after it was staged and words the answer: in the running process when the
/// plugin can be reloaded, otherwise by restarting the host, and writes the result to the activity log. The one place the
/// install use cases (an upload and a registry install) and the removal use case turn the package store's outcome into the
/// answer the panel reads, so the apply rule is decided once.
/// </summary>
public sealed class PluginChangeApplier
{
    /// <summary>The most characters of an id or version written to an audit line; the values come from an uploaded manifest.</summary>
    private const int MaxAuditValueLength = 64;

    /// <summary>Loads the staged version in place of the running one, or unloads a removed plugin.</summary>
    private readonly IPluginHotReloader _reloader;

    /// <summary>Restarts the host for a change that cannot be applied in the running process.</summary>
    private readonly IHostRestarter _restarter;

    /// <summary>Stages the audit line with the result of the apply.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Tells the caller when the change is being applied, and when the host restarts for it.</summary>
    private readonly IPluginChangeReporter _reporter;

    /// <summary>Initializes the applier.</summary>
    /// <param name="reloader">Loads the staged version in place of the running one, or unloads a removed plugin.</param>
    /// <param name="restarter">Restarts the host for a change that cannot be applied in the running process.</param>
    /// <param name="activityLogger">Stages the audit line with the result of the apply.</param>
    /// <param name="db">Saves the audit line.</param>
    /// <param name="reporter">Tells the caller when the change is being applied, and when the host restarts for it.</param>
    public PluginChangeApplier(
        IPluginHotReloader reloader, IHostRestarter restarter, IActivityLogger activityLogger, IHostpanelDbContext db, IPluginChangeReporter reporter)
    {
        _reloader = reloader;
        _restarter = restarter;
        _activityLogger = activityLogger;
        _db = db;
        _reporter = reporter;
    }

    /// <summary>
    /// Applies a package the store accepted. A staged package is swapped in at once when the running plugin can be
    /// reloaded; a package that was installed fresh, and one that cannot be reloaded, are applied by a restart, because
    /// the host's own services are registered per plugin id at startup.
    /// </summary>
    /// <param name="outcome">The store's result, an install or a staged package.</param>
    /// <param name="staged">Whether the package replaces an installed plugin and waits to be swapped in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The answer: reloaded with no restart, or restarting with the staged version; <c>PLUGIN_APPLY_FAILED</c> when the new
    /// version could not be loaded and the previous one was restored.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> AcceptedAsync(PluginPackageResult outcome, bool staged, CancellationToken ct)
    {
        await _reporter.EnterAsync(PluginChangeStage.Apply, ct);
        var reload = staged ? await _reloader.ReloadAsync(outcome.PluginId!, ct) : PluginReloadOutcome.NeedsRestart;
        var line = $"Plugin ID: {Clean(outcome.PluginId)} - Version: {Clean(outcome.PreviousVersion)} to {Clean(outcome.Version)}";
        return await ConcludeAsync(reload, line, staged ? outcome.Version : null, ct);
    }

    /// <summary>Applies a removal: the plugin is unloaded at once when it can be, otherwise it is gone after a restart.</summary>
    /// <param name="pluginId">The id of the removed plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The answer: unloaded with no restart, or restarting.</returns>
    public async Task<Result<PluginActionResultDto>> RemovedAsync(string pluginId, CancellationToken ct)
    {
        await _reporter.EnterAsync(PluginChangeStage.Apply, ct);
        var reload = await _reloader.UnloadAsync(pluginId, ct);
        return await ConcludeAsync(reload, $"Plugin ID: {Clean(pluginId)} - Removal", null, ct);
    }

    /// <summary>Writes the result of the apply to the activity log, starts the restart the change needs and words the answer.</summary>
    /// <param name="reload">How the attempt to apply the change in place ended.</param>
    /// <param name="line">The plugin and the versions, for the audit line.</param>
    /// <param name="stagedVersion">The staged version that replaces the installed one at the restart; <see langword="null"/> for a fresh install and a removal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The answer for the panel.</returns>
    private async Task<Result<PluginActionResultDto>> ConcludeAsync(PluginReloadOutcome reload, string line, string? stagedVersion, CancellationToken ct)
    {
        var result = reload switch
        {
            PluginReloadOutcome.Applied => "Applied In Place",
            PluginReloadOutcome.Failed => "Failed And Restored",
            _ => "Applying By Restart",
        };
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, $"Plugin Change {result} - {line}"), ct);
        await _db.SaveChangesAsync(ct);

        switch (reload)
        {
            case PluginReloadOutcome.Applied:
                return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: false, Reloaded: true));
            case PluginReloadOutcome.Failed:
                return Result<PluginActionResultDto>.FailureCode(HostpanelErrorCodes.PLUGIN_APPLY_FAILED);
            default:
                // After the save, so the line is written whatever happens to the process next.
                await _reporter.EnterAsync(PluginChangeStage.Restart, ct);
                _restarter.Restart();
                return Result<PluginActionResultDto>.Success(
                    new PluginActionResultDto(RequiresRestart: true, StagedVersion: stagedVersion, Restarting: true));
        }
    }

    /// <summary>Makes a value taken from an uploaded manifest safe for a one-line audit text.</summary>
    /// <param name="value">The text from the manifest or the store.</param>
    /// <returns>The cleaned text; empty for <see langword="null"/>.</returns>
    private static string Clean(string? value)
    {
        return UntrustedText.Clean(value, MaxAuditValueLength);
    }
}
