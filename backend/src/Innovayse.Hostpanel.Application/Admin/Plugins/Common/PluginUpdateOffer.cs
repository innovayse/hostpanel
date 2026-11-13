using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one place a registry row is set beside an installed plugin and given its update offer, shared by the registry
/// browser and the update check. The offer is the newest version that is an update and runs on this host
/// (<see cref="PluginUpdateRule.NewestOffer"/>), chosen from the version list this installation is shown
/// (<see cref="RegistryPluginReader"/>). An update the host cannot run is never offered.
/// </summary>
public sealed class PluginUpdateOffer
{
    /// <summary>Reads what this host is shown of a plugin.</summary>
    private readonly RegistryPluginReader _reader;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Initializes the service.</summary>
    /// <param name="reader">Reads what this host is shown of a plugin.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    public PluginUpdateOffer(RegistryPluginReader reader, IInstalledVersionProvider hostVersion)
    {
        _reader = reader;
        _hostVersion = hostVersion;
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

        var read = await _reader.ReadAsync(entry.Id, ct);
        var offer = read.IsFailure
            ? null
            : PluginUpdateRule.NewestOffer(read.Value!.Versions, _hostVersion.Version, package.Manifest.Version, package.StagedVersion);
        if (offer is null)
        {
            return marked;
        }

        return string.Equals(offer.Version, entry.LatestVersion, StringComparison.Ordinal)
            ? marked with { UpdateAvailable = true }
            : marked with { LatestVersion = offer.Version, SdkVersion = offer.SdkVersion, LatestChangelog = offer.Changelog, UpdateAvailable = true };
    }
}
