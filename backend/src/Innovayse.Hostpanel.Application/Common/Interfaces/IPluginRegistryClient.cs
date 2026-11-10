using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The plugin registry: a remote catalogue of installable plugin packages. Implemented in Infrastructure by a client
/// that only talks to the configured registry address. Every failure is a <see cref="Result"/> with a
/// <c>PLUGIN_REGISTRY_*</c> code and no detail of what the registry said or what went wrong on the wire.
/// </summary>
public interface IPluginRegistryClient
{
    /// <summary>Lists one page of the registry's plugins.</summary>
    /// <param name="search">The filters and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page (installed state not yet set), or a <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    public Task<Result<RegistryPluginPageDto>> SearchAsync(RegistryPluginSearch search, CancellationToken ct);

    /// <summary>Asks the registry for the newest version of each of the given plugins.</summary>
    /// <param name="pluginIds">The plugin ids to look up; the registry omits the ones it does not list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugins the registry knows (installed state not yet set), or a <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    public Task<Result<IReadOnlyList<RegistryPluginDto>>> GetLatestAsync(IReadOnlyCollection<string> pluginIds, CancellationToken ct);

    /// <summary>
    /// Resolves one published version of a plugin to its package address and published checksum. For a host only plugin
    /// (<see cref="RegistryPluginVersionRef.Distribution"/>) the call is signed with this installation's registry identity and
    /// carries no licence key.
    /// </summary>
    /// <param name="target">The plugin id and version to resolve, and how the plugin is distributed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="licenceKey">The licence key of a paid or private plugin, sent as a header and never logged; <see langword="null"/> or blank sends none.</param>
    /// <returns>
    /// The release, <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> when the registry does not list it,
    /// <c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c> (the registry answered 401) or <c>PLUGIN_REGISTRY_LICENCE_INVALID</c> (403),
    /// for a host only plugin the refusal of the signed call (<c>PLUGIN_REGISTRY_INSTANCE_*</c>), or another <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public Task<Result<RegistryPluginRelease>> GetReleaseAsync(RegistryPluginVersionRef target, CancellationToken ct, string? licenceKey = null);

    /// <summary>
    /// Downloads a release's package from the registry's own host only, within the configured size limit, into a
    /// temporary file in the plugin staging folder. A host only package is first asked for with a one time download ticket
    /// (a signed call), and the ticket address is then fetched with a signed call, neither carrying a licence key.
    /// </summary>
    /// <param name="release">The release to download.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="licenceKey">The licence key of a paid or private plugin, sent as a header and never logged; <see langword="null"/> or blank sends none.</param>
    /// <returns>
    /// A seekable stream positioned at the start, owned and disposed by the caller; disposing it deletes the file. Or a
    /// <c>PLUGIN_REGISTRY_*</c> failure (<c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c> for a 401, <c>PLUGIN_REGISTRY_LICENCE_INVALID</c>
    /// for a 403), with nothing left on disk.
    /// </returns>
    public Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct, string? licenceKey = null);

    /// <summary>Reads the registry's full page for one plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="licenceKey">The licence key of a paid or private plugin, sent as a header and never logged; <see langword="null"/> or blank sends none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The details, a successful result holding <see langword="null"/> when the registry does not list the plugin (404), or a
    /// <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public Task<Result<RegistryPluginDetails?>> GetPluginAsync(string pluginId, string? licenceKey, CancellationToken ct);

    /// <summary>
    /// Lists every version of a plugin the registry shows. For a host only plugin the call is signed with this installation's
    /// registry identity and carries no licence key.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="licenceKey">The licence key of a paid or private plugin, sent as a header and never logged; <see langword="null"/> or blank sends none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="distribution">Where the plugin is distributed from; <see cref="PluginRegistryDistribution.HostOnly"/> signs the call.</param>
    /// <returns>
    /// The versions in the registry's order (a version with an unknown status or a bad number is left out), or
    /// <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> for a 404, for a host only plugin the refusal of the signed call
    /// (<c>PLUGIN_REGISTRY_INSTANCE_*</c>), or another <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public Task<Result<IReadOnlyList<RegistryPluginVersion>>> ListVersionsAsync(
        string pluginId, string? licenceKey, CancellationToken ct, PluginRegistryDistribution distribution = PluginRegistryDistribution.Download);
}
