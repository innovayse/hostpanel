using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Handles <see cref="InstallPluginCommand"/>: hands the upload to the plugin store, which checks
/// and unpacks it, and records the install in the activity log.
/// </summary>
public sealed class InstallPluginHandler
{
    /// <summary>Checks and unpacks the uploaded archive.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line for the install.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Initializes the handler.</summary>
    /// <param name="store">Checks and unpacks the uploaded archive.</param>
    /// <param name="activityLogger">Stages the audit line for the install.</param>
    /// <param name="db">Saves the audit line.</param>
    public InstallPluginHandler(IPluginPackageStore store, IActivityLogger activityLogger, IHostpanelDbContext db)
    {
        _store = store;
        _activityLogger = activityLogger;
        _db = db;
    }

    /// <summary>
    /// Installs the uploaded plugin and records who installed which package.
    /// </summary>
    /// <param name="command">Command containing the uploaded ZIP stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A result requiring a restart, or <c>PLUGIN_MANIFEST_INVALID</c> when the upload is empty, not
    /// a readable archive, or its <c>plugin.json</c> is missing or malformed;
    /// <c>PLUGIN_SDK_VERSION_UNSUPPORTED</c> when the plugin targets a different SDK version;
    /// one of the manifest rule codes (<c>PLUGIN_MANIFEST_VERSION_UNKNOWN</c>, <c>PLUGIN_SDK_ASSEMBLY_OUTDATED</c>,
    /// <c>PLUGIN_PERMISSION_UNKNOWN</c>, <c>PLUGIN_ADMIN_PERMISSION_DECLARED</c>, <c>PLUGIN_ROUTE_PATH_INVALID</c>,
    /// <c>PLUGIN_ROUTE_DUPLICATE</c>, <c>PLUGIN_LABEL_KEY_MISSING</c>, <c>PLUGIN_SLOT_UNKNOWN</c>,
    /// <c>PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED</c>, <c>PLUGIN_ASSEMBLY_UNREADABLE</c>) when the manifest breaks that rule;
    /// <c>PLUGIN_ID_INVALID</c> when the manifest's id breaks the plugin id rule;
    /// <c>PLUGIN_ARCHIVE_UNSAFE</c> when an entry would write outside the plugin's own folder;
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c> when the archive exceeds the configured limits;
    /// <c>PLUGIN_HOST_ASSEMBLY_REFUSED</c> when it carries a host or framework assembly;
    /// <c>PLUGIN_NOT_TRUSTED</c> when a trusted-package list is set and the archive is not on it;
    /// <c>PLUGIN_BUILT_IN</c> when the id belongs to a plugin shipped with the build.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(InstallPluginCommand command, CancellationToken ct)
    {
        // The validator refuses a missing file before this runs, so Content is set here.
        var outcome = await _store.InstallAsync(new PluginPackageUpload(command.Content!), ct);
        if (outcome.Outcome != PluginPackageOutcome.Installed)
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        await _activityLogger.RecordAsync(
            new ActivityLogEntry(null, $"Installed Plugin - Plugin ID: {outcome.PluginId} - SHA-256: {outcome.Sha256}"), ct);
        await _db.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(new PluginActionResultDto(RequiresRestart: true));
    }
}
