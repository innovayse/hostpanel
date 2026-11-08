using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Hostpanel.Sdk.Packaging.Enums;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallRegistryPlugin;

/// <summary>
/// Handles <see cref="InstallRegistryPluginCommand"/>. The order is the contract: the registry entry is resolved and the
/// trust setting applied before anything is downloaded, the download is checked against the published checksum and
/// against the plugin and version asked for before anything is unpacked, and the install itself is the same call an
/// upload makes, with a valid signature required whatever the environment and whatever "allow unsigned" says.
/// </summary>
public sealed class InstallRegistryPluginHandler
{
    /// <summary>The most characters of an id or version written to an audit line.</summary>
    private const int MaxAuditValueLength = 64;

    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads what an archive claims about itself and which keys the operator added.</summary>
    private readonly IPluginPackageInspector _inspector;

    /// <summary>Checks and unpacks the package.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Stages the audit line.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Reads the trust setting and the licence key, and saves the audit line.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Decrypts the stored licence key.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Makes sure this installation is verified by the registry before a restricted plugin is asked for.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="inspector">Reads what an archive claims about itself and which keys the operator added.</param>
    /// <param name="store">Checks and unpacks the package.</param>
    /// <param name="activityLogger">Stages the audit line.</param>
    /// <param name="db">Reads the trust setting and the licence key, and saves the audit line.</param>
    /// <param name="cipher">Decrypts the stored licence key.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    /// <param name="enrollment">Makes sure this installation is verified by the registry before a restricted plugin is asked for.</param>
    public InstallRegistryPluginHandler(
        IPluginRegistryClient registry,
        IPluginPackageInspector inspector,
        IPluginPackageStore store,
        IActivityLogger activityLogger,
        IHostpanelDbContext db,
        ISecretSettingCipher cipher,
        IInstalledVersionProvider hostVersion,
        RegistryInstanceEnrollment enrollment)
    {
        _registry = registry;
        _inspector = inspector;
        _store = store;
        _activityLogger = activityLogger;
        _db = db;
        _cipher = cipher;
        _hostVersion = hostVersion;
        _enrollment = enrollment;
    }

    /// <summary>Installs or stages the requested registry version and records it.</summary>
    /// <param name="command">The plugin id and version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A result requiring a restart (carrying the staged version for an upgrade); <c>PLUGIN_REGISTRY_DISABLED</c>,
    /// <c>PLUGIN_REGISTRY_UNAVAILABLE</c>, <c>PLUGIN_REGISTRY_TIMED_OUT</c> or <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c> when the
    /// registry cannot be used; <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> when it lists no such version (or has not published it yet);
    /// <c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c> or <c>PLUGIN_REGISTRY_LICENCE_INVALID</c> when the registry wants a good licence key;
    /// for a host only plugin <c>PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED</c>, <c>PLUGIN_REGISTRY_INSTANCE_REVOKED</c> or another
    /// <c>PLUGIN_REGISTRY_INSTANCE_*</c> or <c>PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED</c> failure of the registration or of a signed call;
    /// <c>PLUGIN_REGISTRY_VERSION_YANKED</c> for a withdrawn version, <c>PLUGIN_REGISTRY_VERSION_COMPROMISED</c> for one the
    /// registry rejected as unsafe and <c>PLUGIN_REGISTRY_HOST_INCOMPATIBLE</c> when it does not run on this Hostpanel version;
    /// <c>PLUGIN_REGISTRY_NOT_TRUSTED</c> when the trust setting declines the plugin or its signing key;
    /// <c>PLUGIN_REGISTRY_PACKAGE_REFUSED</c> when the package cannot be fetched safely or is another plugin or version;
    /// <c>PLUGIN_REGISTRY_CHECKSUM_MISMATCH</c> when it does not hash to the published checksum; or any refusal of
    /// <see cref="InstallPlugin.InstallPluginHandler"/> (manifest, signature, size, built-in, downgrade).
    /// </returns>
    public async Task<Result<PluginActionResultDto>> HandleAsync(InstallRegistryPluginCommand command, CancellationToken ct)
    {
        var trust = PluginSettingKeys.ParseRegistryTrust(await _db.Settings
            .AsNoTracking()
            .Where(s => s.Key == PluginSettingKeys.RegistryTrust)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct));

        // Read once and sent with every registry call of this install; it is never logged or put in a refusal.
        var licence = await _db.GetRegistryLicenceAsync(_cipher, command.PluginId, ct);

        // How the plugin is distributed decides how it is asked for, so the registry page is read first. A plugin the
        // registry does not list reads as a public one: the release lookup below then answers not found as it always did.
        var page = await _registry.GetPluginAsync(command.PluginId, licence, ct);
        if (page.IsFailure)
        {
            return Result<PluginActionResultDto>.FailureCode(page.ErrorCode!);
        }

        var distribution = page.Value?.Summary.Distribution ?? PluginRegistryDistribution.Download;
        if (distribution == PluginRegistryDistribution.HostOnly)
        {
            // A restricted plugin is given only to a verified installation, and a licence key has no part in it: neither is sent.
            var enrolled = await _enrollment.EnsureVerifiedAsync(force: false, ct);
            if (enrolled.IsFailure)
            {
                return Result<PluginActionResultDto>.FailureCode(enrolled.ErrorCode!);
            }

            licence = null;
        }

        var found = await _registry.GetReleaseAsync(new RegistryPluginVersionRef(command.PluginId, command.Version, distribution), ct, licence);
        if (found.IsFailure)
        {
            return Result<PluginActionResultDto>.FailureCode(found.ErrorCode!);
        }

        var release = found.Value!;
        if (!string.Equals(release.PluginId, command.PluginId, StringComparison.Ordinal)
            || !string.Equals(release.Version, command.Version, StringComparison.Ordinal))
        {
            // The registry answered for something other than what was asked.
            return Result<PluginActionResultDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID);
        }

        var listed = await _registry.ListVersionsAsync(command.PluginId, licence, ct, distribution);
        if (listed.IsFailure)
        {
            return Result<PluginActionResultDto>.FailureCode(listed.ErrorCode!);
        }

        // A release the registry serves but does not describe cannot be checked, so it is not installed.
        // A version listed more than once is judged by its strictest row.
        var described = listed.Value!
            .Where(v => string.Equals(v.Version, command.Version, StringComparison.Ordinal))
            .OrderByDescending(v => v.Status.Strictness())
            .FirstOrDefault();
        if (described is null)
        {
            return Result<PluginActionResultDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        }

        var unavailable = described.Status switch
        {
            PluginRegistryVersionStatus.Yanked => HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_YANKED,
            PluginRegistryVersionStatus.Rejected => HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_COMPROMISED,
            PluginRegistryVersionStatus.InReview => HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND,
            _ => null,
        };
        if (unavailable is not null)
        {
            return await RefuseAsync(command, $"version is {described.Status}", unavailable, null, ct);
        }

        if (!PluginHostCompatibility.IsCompatible(_hostVersion.Version, described.MinHostVersion, described.MaxHostVersion))
        {
            return await RefuseAsync(command, "host version is outside the supported range", HostpanelErrorCodes.PLUGIN_REGISTRY_HOST_INCOMPATIBLE, null, ct);
        }

        if (trust == PluginRegistryTrust.VerifiedOnly && !release.Verified)
        {
            return await RefuseAsync(command, "not verified", HostpanelErrorCodes.PLUGIN_REGISTRY_NOT_TRUSTED, null, ct);
        }

        var download = await _registry.DownloadAsync(release, ct, licence);
        if (download.IsFailure)
        {
            return Result<PluginActionResultDto>.FailureCode(download.ErrorCode!);
        }

        // Disposing the stream deletes the temporary file, on every path out of here.
        await using var package = download.Value!;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(package, ct));
        if (!string.Equals(sha256, release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return await RefuseAsync(command, "checksum mismatch", HostpanelErrorCodes.PLUGIN_REGISTRY_CHECKSUM_MISMATCH, sha256, ct);
        }

        package.Seek(0, SeekOrigin.Begin);
        var header = await _inspector.ReadHeaderAsync(package, ct);
        if (!string.Equals(header.PluginId, command.PluginId, StringComparison.Ordinal))
        {
            return await RefuseAsync(command, "package is another plugin", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED, sha256, ct);
        }

        if (!string.Equals(header.Version, command.Version, StringComparison.Ordinal))
        {
            return await RefuseAsync(command, "package is another version", HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED, sha256, ct);
        }

        if (trust == PluginRegistryTrust.OwnKeysOnly && (header.KeyId is null || !_inspector.IsOperatorKey(header.KeyId)))
        {
            return await RefuseAsync(command, "signing key is not an operator key", HostpanelErrorCodes.PLUGIN_REGISTRY_NOT_TRUSTED, sha256, ct);
        }

        // A valid signature is required here whatever the environment: the "allow unsigned" setting and a Development
        // host are for packages an operator uploads by hand, never for anything fetched from a remote catalogue.
        package.Seek(0, SeekOrigin.Begin);
        var outcome = await _store.InstallAsync(new PluginPackageUpload(package, AllowUnsigned: false, ForceDowngrade: command.AllowDowngrade, RequireSignature: true), ct);
        if (outcome.Outcome == PluginPackageOutcome.DowngradeRefused)
        {
            return await RefuseAsync(
                command, "older than the installed version", PluginPackageErrors.CodeFor(outcome), outcome.Sha256, ct);
        }

        if (outcome.Outcome is not (PluginPackageOutcome.Installed or PluginPackageOutcome.Staged))
        {
            return Result<PluginActionResultDto>.FailureCode(PluginPackageErrors.CodeFor(outcome));
        }

        // The audit line and the answer carry what the store really installed, not what was asked for.
        var staged = outcome.Outcome == PluginPackageOutcome.Staged;
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                null,
                $"{Describe(outcome, staged)} - Plugin ID: {Clean(outcome.PluginId)} - Version: {Clean(outcome.Version)} - SHA-256: {outcome.Sha256}"),
            ct);
        await _db.SaveChangesAsync(ct);

        return Result<PluginActionResultDto>.Success(
            new PluginActionResultDto(RequiresRestart: true, StagedVersion: staged ? outcome.Version : null));
    }

    /// <summary>Words the audit line title of an accepted registry install.</summary>
    /// <param name="outcome">The store result.</param>
    /// <param name="staged">Whether the package waits for the restart.</param>
    /// <returns>The title: an install, a staged upgrade, a staged replacement or a forced downgrade.</returns>
    private static string Describe(PluginPackageResult outcome, bool staged)
    {
        if (!staged)
        {
            return "Installed Plugin From Registry";
        }

        return PluginVersion.Compare(outcome.PreviousVersion, outcome.Version) switch
        {
            PluginVersionChange.Newer => "Staged Plugin Upgrade From Registry",
            PluginVersionChange.Same => "Staged Plugin Replacement From Registry",
            _ => "Forced Plugin Downgrade From Registry",
        };
    }

    /// <summary>Audits a refusal that stopped a package from being installed and answers with its code.</summary>
    /// <param name="command">The install request, whose id and version have the validated shape.</param>
    /// <param name="reason">A fixed phrase naming why; never text from the registry.</param>
    /// <param name="code">The error code to answer with.</param>
    /// <param name="sha256">The digest of the downloaded package when one was downloaded; <see langword="null"/> otherwise.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The failure.</returns>
    private async Task<Result<PluginActionResultDto>> RefuseAsync(
        InstallRegistryPluginCommand command, string reason, string code, string? sha256, CancellationToken ct)
    {
        // A refused registry install is security relevant (it is how an altered or untrusted package would arrive).
        await _activityLogger.RecordAsync(
            new ActivityLogEntry(
                null,
                $"Refused Plugin From Registry - Plugin ID: {command.PluginId} - Version: {command.Version} - Reason: {reason}"
                + (sha256 is null ? string.Empty : $" - SHA-256: {sha256}")),
            ct);
        await _db.SaveChangesAsync(ct);
        return Result<PluginActionResultDto>.FailureCode(code);
    }

    /// <summary>Makes a value the store read from the package safe for a one-line audit text.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The cleaned value.</returns>
    private static string Clean(string? value)
    {
        return UntrustedText.Clean(value, MaxAuditValueLength);
    }
}
