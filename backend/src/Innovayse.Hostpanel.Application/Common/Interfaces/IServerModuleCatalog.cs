using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The control panel modules a server can be given: the ids the host knows plus the ones installed panel plugins declare. The one
/// reader of the plugins' <c>serverModules</c> lists for the admin side.
/// </summary>
public interface IServerModuleCatalog
{
    /// <summary>Lists every module an administrator can pick, built in ones first.</summary>
    /// <returns>The modules, each id once.</returns>
    public IReadOnlyList<ServerModuleOptionDto> List();

    /// <summary>Finds the spelling to store for a module id: the built in spelling when one matches ignoring case, else the plugin's declared one.</summary>
    /// <param name="module">The id as the administrator wrote it.</param>
    /// <param name="stored">The module the edited server is already stored with, when one is edited. It is accepted unchanged even
    /// when no plugin serves it any more, so a server saved before its panel plugin was removed can still be edited.</param>
    /// <returns>The id to store, or <see langword="null"/> when neither the host, an installed plugin nor the server's own stored module knows it.</returns>
    public string? Canonical(string module, string? stored = null);
}
