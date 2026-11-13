using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one place a registry row is set beside an installed plugin and given its update offer, shared by the registry
/// browser and the update check. The offer is the newest version that is an update and runs on this host
/// (<see cref="PluginUpdateRule.NewestOffer"/>), chosen from the version list this installation may see: the one the registry
/// shows to this host with the stored licence key, or, for a host only plugin, the signed list of a verified installation.
/// An update the host cannot run is never offered.
/// </summary>
/// <remarks>
/// The service is scoped, so it serves one request: it reads each plugin's licence key once, confirms the installation
/// once, and stops asking the registry after the first time the registry could not be reached.
/// </remarks>
public sealed class PluginUpdateOffer
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the stored licence keys.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Decrypts the stored licence keys.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Makes sure this installation is verified by the registry before the versions of a host only plugin are asked for.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>The licence keys read in this request, by plugin id; a plugin without a key is stored as <see langword="null"/>.</summary>
    private readonly Dictionary<string, string?> _licences = new(StringComparer.Ordinal);

    /// <summary>Whether this installation is verified, once asked in this request.</summary>
    private bool? _verified;

    /// <summary>Whether the registry could not be reached in this request, after which it is not asked again.</summary>
    private bool _registryDown;

    /// <summary>Initializes the service.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="db">Reads the stored licence keys.</param>
    /// <param name="cipher">Decrypts the stored licence keys.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    /// <param name="enrollment">Makes sure this installation is verified by the registry.</param>
    public PluginUpdateOffer(
        IPluginRegistryClient registry,
        IHostpanelDbContext db,
        ISecretSettingCipher cipher,
        IInstalledVersionProvider hostVersion,
        RegistryInstanceEnrollment enrollment)
    {
        _registry = registry;
        _db = db;
        _cipher = cipher;
        _hostVersion = hostVersion;
        _enrollment = enrollment;
    }

    /// <summary>Reads the licence key stored for a plugin, once per request.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The key, or <see langword="null"/> when none is stored.</returns>
    public async Task<string?> LicenceAsync(string pluginId, CancellationToken ct)
    {
        if (!_licences.TryGetValue(pluginId, out var licence))
        {
            licence = await _db.GetRegistryLicenceAsync(_cipher, pluginId, ct);
            _licences[pluginId] = licence;
        }

        return licence;
    }

    /// <summary>Marks a registry row as installed and, when an update that runs on this host exists, offers it.</summary>
    /// <param name="entry">The registry row.</param>
    /// <param name="package">The installed plugin with the same id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The row with the installed state set. When an update is offered, <c>UpdateAvailable</c> is true and
    /// <c>LatestVersion</c>, <c>SdkVersion</c> and <c>LatestChangelog</c> describe the offered version, which is the one an
    /// update installs. No offer is made when the versions cannot be read, because an update that runs here cannot be proven.
    /// </returns>
    public async Task<RegistryPluginDto> MarkAsync(RegistryPluginDto entry, InstalledPluginPackage package, CancellationToken ct)
    {
        var marked = entry with { Installed = true, InstalledVersion = package.Manifest.Version };
        if (!PluginUpdateRule.IsUpdate(package.Manifest.Version, package.StagedVersion, entry.LatestVersion))
        {
            return marked;
        }

        var versions = await ListVersionsAsync(entry.Id, ct);
        var offer = versions is null
            ? null
            : PluginUpdateRule.NewestOffer(versions, _hostVersion.Version, package.Manifest.Version, package.StagedVersion);
        if (offer is null)
        {
            return marked;
        }

        return string.Equals(offer.Version, entry.LatestVersion, StringComparison.Ordinal)
            ? marked with { UpdateAvailable = true }
            : marked with { LatestVersion = offer.Version, SdkVersion = offer.SdkVersion, LatestChangelog = offer.Changelog, UpdateAvailable = true };
    }

    /// <summary>
    /// Reads the versions of a plugin the way the details page does: the plugin's page with the stored licence key says how it is
    /// distributed, a host only plugin first needs the installation to be verified, and then the versions are listed.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The versions; <see langword="null"/> when they cannot be read.</returns>
    private async Task<IReadOnlyList<RegistryPluginVersion>?> ListVersionsAsync(string pluginId, CancellationToken ct)
    {
        if (_registryDown)
        {
            return null;
        }

        var licence = await LicenceAsync(pluginId, ct);
        var page = await _registry.GetPluginAsync(pluginId, licence, ct);
        if (page.IsFailure)
        {
            NoteFailure(page.ErrorCode!);
            return null;
        }

        var details = page.Value;
        if (details is null || !string.Equals(details.Summary.Id, pluginId, StringComparison.Ordinal))
        {
            return null;
        }

        var distribution = details.Summary.Distribution;
        if (distribution == PluginRegistryDistribution.HostOnly && !await IsVerifiedAsync(ct))
        {
            return null;
        }

        var listed = await _registry.ListVersionsAsync(pluginId, licence, ct, distribution);
        if (listed.IsFailure)
        {
            NoteFailure(listed.ErrorCode!);
            return null;
        }

        return listed.Value;
    }

    /// <summary>Tells whether this installation is verified by the registry, asking once per request.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the installation is verified.</returns>
    private async Task<bool> IsVerifiedAsync(CancellationToken ct)
    {
        _verified ??= (await _enrollment.EnsureVerifiedAsync(force: false, ct)).IsSuccess;
        return _verified.Value;
    }

    /// <summary>Remembers that the registry cannot be reached, so the rest of the request does not ask it again.</summary>
    /// <param name="errorCode">The failure code of the registry call.</param>
    private void NoteFailure(string errorCode)
    {
        if (errorCode is HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED
            or HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE
            or HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT
            or HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID)
        {
            _registryDown = true;
        }
    }
}
