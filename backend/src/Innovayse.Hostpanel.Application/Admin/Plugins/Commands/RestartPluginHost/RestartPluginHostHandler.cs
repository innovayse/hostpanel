using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RestartPluginHost;

/// <summary>
/// Handles <see cref="RestartPluginHostCommand"/>: records who asked for the restart in the activity log, saves it, and
/// only then starts the host's graceful stop, so a restart that is audited is the only restart there is.
/// </summary>
public sealed class RestartPluginHostHandler
{
    /// <summary>Stages the audit line for the restart.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line before the host starts to stop.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Starts the graceful stop of the host.</summary>
    private readonly IHostRestarter _restarter;

    /// <summary>Tells the caller that the host is restarting, so the panel waits for it to come back.</summary>
    private readonly IPluginChangeProgress _reporter;

    /// <summary>Initializes the handler.</summary>
    /// <param name="activityLogger">Stages the audit line for the restart.</param>
    /// <param name="db">Saves the audit line before the host starts to stop.</param>
    /// <param name="restarter">Starts the graceful stop of the host.</param>
    /// <param name="reporter">Tells the caller that the host is restarting, so the panel waits for it to come back.</param>
    public RestartPluginHostHandler(IActivityLogger activityLogger, IHostpanelDbContext db, IHostRestarter restarter, IPluginChangeProgress reporter)
    {
        _activityLogger = activityLogger;
        _db = db;
        _restarter = restarter;
        _reporter = reporter;
    }

    /// <summary>Records the restart, saves it and starts the host's graceful stop.</summary>
    /// <param name="command">The restart request; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once the stop has started; the process exits after the running requests finish.</returns>
    public async Task<Result> HandleAsync(RestartPluginHostCommand command, CancellationToken ct)
    {
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, "Restarted Plugin Host"), ct);

        // Saved before the stop starts: once the process exits nothing can record who asked for it.
        await _db.SaveChangesAsync(ct);

        await _reporter.EnterAsync(PluginChangeStage.Restart, ct);
        _restarter.Restart();
        return Result.Success();
    }
}
