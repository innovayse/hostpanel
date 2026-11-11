using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryPlugin;

/// <summary>
/// Handles <see cref="GetRegistryPluginQuery"/>: asks the registry for the plugin's page and its versions, sending the
/// licence key stored for the plugin, and sets the answer beside what is installed here and the running host version.
/// </summary>
public sealed class GetRegistryPluginHandler
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the plugins installed on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Reads the stored licence key.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Decrypts the stored licence key.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Makes sure this installation is verified by the registry before the versions of a restricted plugin are asked for.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="store">Reads the plugins installed on disk.</param>
    /// <param name="db">Reads the stored licence key.</param>
    /// <param name="cipher">Decrypts the stored licence key.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    /// <param name="enrollment">Makes sure this installation is verified by the registry before the versions of a restricted plugin are asked for.</param>
    public GetRegistryPluginHandler(
        IPluginRegistryClient registry,
        IPluginPackageStore store,
        IHostpanelDbContext db,
        ISecretSettingCipher cipher,
        IInstalledVersionProvider hostVersion,
        RegistryInstanceEnrollment enrollment)
    {
        _registry = registry;
        _store = store;
        _db = db;
        _cipher = cipher;
        _hostVersion = hostVersion;
        _enrollment = enrollment;
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
        var licence = await _db.GetRegistryLicenceAsync(_cipher, query.PluginId, ct);

        var page = await _registry.GetPluginAsync(query.PluginId, licence, ct);
        if (page.IsFailure)
        {
            return Result<RegistryPluginDetailsDto>.FailureCode(page.ErrorCode!);
        }

        var details = page.Value;
        if (details is null || !string.Equals(details.Summary.Id, query.PluginId, StringComparison.Ordinal))
        {
            return Result<RegistryPluginDetailsDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        }

        // A restricted plugin lists its versions only to a verified installation, with no licence key.
        var distribution = details.Summary.Distribution;
        if (distribution == PluginRegistryDistribution.HostOnly)
        {
            var enrolled = await _enrollment.EnsureVerifiedAsync(force: false, ct);
            if (enrolled.IsFailure)
            {
                return Result<RegistryPluginDetailsDto>.FailureCode(enrolled.ErrorCode!);
            }
        }

        var listed = await _registry.ListVersionsAsync(query.PluginId, licence, ct, distribution);
        if (listed.IsFailure)
        {
            return Result<RegistryPluginDetailsDto>.FailureCode(listed.ErrorCode!);
        }

        var summary = details.Summary;
        var latest = string.IsNullOrEmpty(summary.LatestVersion) ? details.Latest?.Version : summary.LatestVersion;
        if (string.IsNullOrEmpty(latest))
        {
            latest = null;
        }

        var installed = (await _store.ListAsync(ct))
            .FirstOrDefault(p => string.Equals(p.Manifest.Id, query.PluginId, StringComparison.OrdinalIgnoreCase));
        var update = installed is not null
            && latest is not null
            && PluginUpdateRule.IsUpdate(installed.Manifest.Version, installed.StagedVersion, latest);

        var host = _hostVersion.Version;
        var versions = listed.Value!
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
