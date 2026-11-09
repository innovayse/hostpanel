using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.InspectPluginPackage;

/// <summary>
/// Handles <see cref="InspectPluginPackageQuery"/>: reads the manifest out of the upload and answers what it asks for and
/// whether this Hostpanel version is inside its supported range, the same comparison the install makes.
/// </summary>
public sealed class InspectPluginPackageHandler
{
    /// <summary>Reads the manifest out of the archive.</summary>
    private readonly IPluginPackageInspector _inspector;

    /// <summary>The version of the running Hostpanel build.</summary>
    private readonly IInstalledVersionProvider _hostVersion;

    /// <summary>Initializes the handler.</summary>
    /// <param name="inspector">Reads the manifest out of the archive.</param>
    /// <param name="hostVersion">The version of the running Hostpanel build.</param>
    public InspectPluginPackageHandler(IPluginPackageInspector inspector, IInstalledVersionProvider hostVersion)
    {
        _inspector = inspector;
        _hostVersion = hostVersion;
    }

    /// <summary>Reads the package's manifest.</summary>
    /// <param name="query">The upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preview; <c>PLUGIN_MANIFEST_INVALID</c> when the archive or its <c>plugin.json</c> cannot be read.</returns>
    public async Task<Result<PluginPackagePreviewDto>> HandleAsync(InspectPluginPackageQuery query, CancellationToken ct)
    {
        var manifest = await _inspector.ReadManifestAsync(query.Content!, ct);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
        {
            return Result<PluginPackagePreviewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_MANIFEST_INVALID);
        }

        var host = _hostVersion.Version;
        return Result<PluginPackagePreviewDto>.Success(new PluginPackagePreviewDto(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Author,
            manifest.Description,
            manifest.HostAccess,
            manifest.Capabilities,
            string.IsNullOrWhiteSpace(manifest.Changelog) ? null : manifest.Changelog,
            NullIfBlank(manifest.MinHostVersion),
            NullIfBlank(manifest.MaxHostVersion),
            host,
            PluginHostCompatibility.IsCompatible(host, manifest.MinHostVersion, manifest.MaxHostVersion)));
    }

    /// <summary>Treats a blank bound as an absent one, the way the compatibility check does.</summary>
    /// <param name="value">The bound as written.</param>
    /// <returns>The bound, or <see langword="null"/> when blank.</returns>
    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
