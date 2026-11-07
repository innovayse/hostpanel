using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
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
    Task<Result<RegistryPluginPageDto>> SearchAsync(RegistryPluginSearch search, CancellationToken ct);

    /// <summary>Asks the registry for the newest version of each of the given plugins.</summary>
    /// <param name="pluginIds">The plugin ids to look up; the registry omits the ones it does not list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugins the registry knows (installed state not yet set), or a <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    Task<Result<IReadOnlyList<RegistryPluginDto>>> GetLatestAsync(IReadOnlyCollection<string> pluginIds, CancellationToken ct);

    /// <summary>Resolves one published version of a plugin to its package address and published checksum.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="version">The version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The release, <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> when the registry does not list it, or another <c>PLUGIN_REGISTRY_*</c> failure.</returns>
    Task<Result<RegistryPluginRelease>> GetReleaseAsync(string pluginId, string version, CancellationToken ct);

    /// <summary>
    /// Downloads a release's package from the registry's own host only, within the configured size limit, into a
    /// temporary file in the plugin staging folder.
    /// </summary>
    /// <param name="release">The release to download.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A seekable stream positioned at the start, owned and disposed by the caller; disposing it deletes the file. Or a
    /// <c>PLUGIN_REGISTRY_*</c> failure, with nothing left on disk.
    /// </returns>
    Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct);
}
