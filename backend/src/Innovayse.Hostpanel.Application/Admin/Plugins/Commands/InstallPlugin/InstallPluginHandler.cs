using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using PluginVersionChange = Hostpanel.Sdk.Packaging.Enums.PluginVersionChange;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Handles <see cref="InstallPluginCommand"/>: hands the upload to the plugin store, which checks
/// and unpacks or stages it, and records the install or the staged upgrade in the activity log.
/// </summary>
public sealed class InstallPluginHandler
{
    /// <summary>The most characters of an id or version written to an audit line; the values come from an uploaded manifest.</summary>
    private const int MaxAuditValueLength = 64;

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
    /// A result requiring a restart (carrying <c>StagedVersion</c> when the upload is an upgrade or a forced downgrade
    /// that waits for the restart), <c>PLUGIN_DOWNGRADE_REFUSED</c> when the upload is older than the installed version
    /// and the downgrade was not forced, or <c>PLUGIN_MANIFEST_INVALID</c> when the upload is empty, not
    /// a readable archive, or its <c>plugin.json</c> is missing or malformed;
    /// <c>PLUGIN_SDK_VERSION_UNSUPPORTED</c> when the plugin targets a different SDK version;
    /// one of the manifest rule codes (<c>PLUGIN_MANIFEST_VERSION_UNKNOWN</c>, <c>PLUGIN_SDK_ASSEMBLY_OUTDATED</c>,
    /// <c>PLUGIN_PERMISSION_UNKNOWN</c>, <c>PLUGIN_ADMIN_PERMISSION_DECLARED</c>, <c>PLUGIN_ROUTE_PATH_INVALID</c>,
    /// <c>PLUGIN_ROUTE_DUPLICATE</c>, <c>PLUGIN_LABEL_KEY_MISSING</c>, <c>PLUGIN_SLOT_UNKNOWN</c>,
    /// <c>PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED</c>, <c>PLUGIN_ASSEMBLY_UNREADABLE</c>) when the manifest breaks that rule;
    /// <c>PLUGIN_ID_INVALID</c> when the manifest's id breaks the plugin id rule;
    /// <c>PLUGIN_ARCHIVE_UNSAFE</c> when an entry would write outside the plugin's own folder;
    /// <c>PLUGIN_ARCHIVE_UNWRITABLE</c> when an entry cannot be written into the unpack folder (a file and a folder share a name);
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c> when the archive exceeds the configured limits;
    /// <c>PLUGIN_HOST_ASSEMBLY_REFUSED</c> when it carries a host or framework assembly;
    /// <c>PLUGIN_ASSEMBLY_CONFLICT</c> when an assembly file name is already shipped by another installed or staged plugin;
    /// <c>PLUGIN_NOT_TRUSTED</c> when a trusted-package list is set and the archive is not on it;
    /// one of <c>PLUGIN_UNSIGNED</c>, <c>PLUGIN_SIGNATURE_MALFORMED</c>, <c>PLUGIN_SIGNATURE_KEY_UNKNOWN</c> or
    /// <c>PLUGIN_SIGNATURE_INVALID</c> when the host enforces signatures, the setting "allow unsigned plugins" is off and
    /// the package fails that check;
    /// <c>PLUGIN_BUILT_IN</c> when the id belongs to a plugin shipped with the build.
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(InstallPluginCommand command, CancellationToken ct)
    {
        // The validator refuses a missing file before this runs, so Content is set here.
        // Read at the moment of the upload, so turning the setting on or off applies to the very next install.
        var allowUnsigned = PluginSettingKeys.IsOn(await _db.Settings
            .AsNoTracking()
            .Where(s => s.Key == PluginSettingKeys.AllowUnsigned)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct));

        var outcome = await _store.InstallAsync(new PluginPackageUpload(command.Content!, allowUnsigned, command.ForceDowngrade), ct);
        if (outcome.Outcome == PluginPackageOutcome.DowngradeRefused)
        {
            // A refused downgrade is a security-relevant attempt (it is how a fixed flaw is brought back), so it is audited too.
            await _activityLogger.RecordAsync(
                new ActivityLogEntry(
                    null,
                    $"Refused Plugin Downgrade - Plugin ID: {Clean(outcome.PluginId)} - Version: {Clean(outcome.PreviousVersion)} to {Clean(outcome.Version)} - SHA-256: {outcome.Sha256}"),
                ct);
            await _db.SaveChangesAsync(ct);
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        if (outcome.Outcome is not (PluginPackageOutcome.Installed or PluginPackageOutcome.Staged))
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        var staged = outcome.Outcome == PluginPackageOutcome.Staged;
        await _activityLogger.RecordAsync(new ActivityLogEntry(null, DescribeAccepted(outcome, staged)), ct);
        await _db.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(
            new PluginActionResultDto(RequiresRestart: true, StagedVersion: staged ? outcome.Version : null));
    }

    /// <summary>Writes the activity log line of an install or a staged package.</summary>
    /// <param name="outcome">The store's result, an install or a staged package.</param>
    /// <param name="staged">Whether the package waits for the restart.</param>
    /// <returns>The line: an install, a staged upgrade, a staged replacement of the same version or a forced downgrade.</returns>
    private static string DescribeAccepted(PluginPackageResult outcome, bool staged)
    {
        if (!staged)
        {
            return $"Installed Plugin - Plugin ID: {Clean(outcome.PluginId)} - SHA-256: {outcome.Sha256}";
        }

        var title = PluginVersion.Compare(outcome.PreviousVersion, outcome.Version) switch
        {
            PluginVersionChange.Newer => "Staged Plugin Upgrade",
            PluginVersionChange.Same => "Staged Plugin Replacement",
            _ => "Forced Plugin Downgrade",
        };
        return $"{title} - Plugin ID: {Clean(outcome.PluginId)} - Version: {Clean(outcome.PreviousVersion)} to {Clean(outcome.Version)} - SHA-256: {outcome.Sha256}";
    }

    /// <summary>
    /// Makes a value taken from an uploaded manifest safe for a one-line audit text: control characters (a line break
    /// would forge a second line) become spaces and the length is capped.
    /// </summary>
    /// <param name="value">The text from the manifest or the store.</param>
    /// <returns>The cleaned text; empty for <see langword="null"/>.</returns>
    private static string Clean(string? value)
    {
        return UntrustedText.Clean(value, MaxAuditValueLength);
    }
}
