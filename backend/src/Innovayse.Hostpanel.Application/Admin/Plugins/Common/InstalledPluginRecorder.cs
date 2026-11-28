using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Writes the install record of a plugin package the store accepted: the first install creates it, a later accepted
/// package updates it. The one place both install use cases (an upload and a registry install) record what they did, so
/// the record is built the same way whichever door the package came through. It stages the change on the context and
/// leaves the save to the caller, which saves it together with the audit line.
/// </summary>
public sealed class InstalledPluginRecorder
{
    /// <summary>The Admin module's data context the record is staged on.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the install moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>The administrator who installs, stamped on the first install of a plugin.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Tells whether the key that signed a package is one the operator added.</summary>
    private readonly IPluginPackageInspector _inspector;

    /// <summary>Initializes the recorder.</summary>
    /// <param name="context">The Admin module's data context the record is staged on.</param>
    /// <param name="clock">The clock the install moment is read from.</param>
    /// <param name="caller">The administrator who installs, stamped on the first install of a plugin.</param>
    /// <param name="inspector">Tells whether the key that signed a package is one the operator added.</param>
    public InstalledPluginRecorder(
        IHostpanelDbContext context, IClock clock, ICurrentUserService caller, IPluginPackageInspector inspector)
    {
        _context = context;
        _clock = clock;
        _caller = caller;
        _inspector = inspector;
    }

    /// <summary>Stages the install record for an accepted package: creates it, or updates the one the plugin already has.</summary>
    /// <param name="accepted">The store's result for an installed or staged package.</param>
    /// <param name="source">Where the package came from: an upload by hand or the registry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="registryKey">The key the registry published for the signer, when the package was trusted through it; <see langword="null"/> when the host's configured keys vouched for it.</param>
    /// <returns>A task that completes when the record is staged; nothing is saved.</returns>
    /// <exception cref="ArgumentException">The result names no plugin or digest.</exception>
    public async Task RecordAsync(PluginPackageResult accepted, PluginSource source, CancellationToken ct, PluginSignerKey? registryKey = null)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        if (accepted.PluginId is not { } pluginId || accepted.Sha256 is not { } sha256)
        {
            throw new ArgumentException("Only an installed or staged package, which names its plugin and digest, is recorded.", nameof(accepted));
        }


        // Kept only when the store verified the signature with that very key, so a package that a configured key vouched for
        // (or an upload) leaves no registry key on the record and is checked against the configuration alone from then on.
        var pinned = registryKey is not null && string.Equals(registryKey.KeyId, accepted.SignerKeyId, StringComparison.Ordinal)
            ? registryKey.PublicKey
            : null;
        var package = new PluginPackageFacts(source, SignerOf(accepted.SignerKeyId), accepted.SignerKeyId, accepted.SizeBytes, sha256, pinned);
        var record = await _context.InstalledPlugins.FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);
        var now = _clock.UtcNow;

        if (record is null)
        {
            _context.InstalledPlugins.Add(InstalledPlugin.Create(
                pluginId, package, _caller.UserId, await _caller.GetUserNameAsync(ct), now));
            return;
        }

        record.RecordUpload(package, now);
    }

    /// <summary>Tells who vouched for a package from the key that signed it.</summary>
    /// <param name="keyId">The verified signing key id; <see langword="null"/> for an unsigned package.</param>
    /// <returns>The operator's own key, a maintainers' key (a verified publisher), or none.</returns>
    private PluginSignerKind SignerOf(string? keyId)
    {
        if (keyId is null)
        {
            return PluginSignerKind.Unsigned;
        }

        return _inspector.IsOperatorKey(keyId) ? PluginSignerKind.OwnKey : PluginSignerKind.VerifiedPublisher;
    }
}
