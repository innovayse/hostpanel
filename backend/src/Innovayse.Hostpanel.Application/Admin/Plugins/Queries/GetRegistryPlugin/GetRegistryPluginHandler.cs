using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryPlugin;

/// <summary>
/// Handles <see cref="GetRegistryPluginQuery"/>: reads the plugin's page and its versions as this host is shown them
/// (<see cref="RegistryPluginReader"/>, with the licence key stored for the plugin), and sets the answer beside what is
/// installed here and the running host version.
/// </summary>
public sealed class GetRegistryPluginHandler
{
    /// <summary>Reads what this host is shown of a registry plugin.</summary>
    private readonly RegistryPluginReader _reader;

    /// <summary>Reads the plugins installed on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Initializes the handler.</summary>
    /// <param name="reader">Reads what this host is shown of a registry plugin.</param>
    /// <param name="store">Reads the plugins installed on disk.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    public GetRegistryPluginHandler(RegistryPluginReader reader, IPluginPackageStore store, IInstalledVersionProvider hostVersion)
    {
        _reader = reader;
        _store = store;
        _hostVersion = hostVersion;
    }

    /// <summary>Returns the details of one registry plugin.</summary>
    /// <param name="query">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The details; <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> when the registry does not list the plugin; <c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c>
    /// or <c>PLUGIN_REGISTRY_LICENCE_INVALID</c> when the registry refuses to show it without a good key; for a host only plugin
    /// <c>PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED</c> and the other <c>PLUGIN_REGISTRY_INSTANCE_*</c> failures of the registration or of a
    /// signed call; or another
    /// <c>PLUGIN_REGISTRY_*</c> failure when the registry cannot be used.
    /// </returns>
    public async Task<Result<RegistryPluginDetailsDto>> HandleAsync(GetRegistryPluginQuery query, CancellationToken ct)
    {
        var read = await _reader.ReadAsync(query.PluginId, ct);
        if (read.IsFailure)
        {
            return Result<RegistryPluginDetailsDto>.FailureCode(read.ErrorCode!);
        }

        var (details, licence, listed) = read.Value!;

        var summary = details.Summary;
        var latest = string.IsNullOrEmpty(summary.LatestVersion) ? details.Latest?.Version : summary.LatestVersion;
        if (string.IsNullOrEmpty(latest))
        {
            latest = null;
        }

        var installed = (await _store.ListAsync(ct))
            .FirstOrDefault(p => string.Equals(p.Manifest.Id, query.PluginId, StringComparison.OrdinalIgnoreCase));
        var host = _hostVersion.Version;

        // The update to offer is the newest version that runs on this host, which is not always the registry's newest;
        // the page then updates to it and names it as the latest.
        var offer = installed is null
            ? null
            : PluginUpdateRule.NewestOffer(listed, host, installed.Manifest.Version, installed.StagedVersion);
        var update = offer is not null;
        if (offer is not null)
        {
            latest = offer.Version;
        }

        var versions = listed
            .OrderByDescending(v => v.PublishedAt)
            .Select(v => new RegistryPluginVersionDto(
                v.Version,
                v.PublishedAt,
                v.Status,
                v.StatusReason,
                v.ReplacementVersion,
                v.Changelog,
                v.MinHostVersion,
                v.MaxHostVersion,
                PluginHostCompatibility.IsCompatible(host, v.MinHostVersion, v.MaxHostVersion),
                v.Permissions,
                v.Screenshots))
            .ToList();

        return Result<RegistryPluginDetailsDto>.Success(new RegistryPluginDetailsDto(
            summary.Id,
            summary.Name,
            summary.Author,
            summary.Description,
            summary.IconUrl,
            summary.Category,
            summary.Pricing,
            summary.Verified,
            latest,
            installed is not null,
            installed?.Manifest.Version,
            installed?.StagedVersion,
            update,
            summary.Pricing != PluginRegistryPricing.Free,
            licence is not null,
            details.BuyUrl,
            details.DocumentationUrl,
            details.SupportUrl,
            details.Repository,
            details.Licence,
            versions));
    }
}
