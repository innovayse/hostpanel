using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads what an archive claims about itself before it is installed, and knows which signing keys the operator added.
/// The claims are not trusted here: the package store still verifies the signature, and a forged claim only fails
/// earlier or later there.
/// </summary>
public interface IPluginPackageInspector
{
    /// <summary>Reads the plugin id and the signing key id out of an archive without unpacking it to disk.</summary>
    /// <param name="package">A seekable ZIP stream; the caller seeks it back to the start afterwards.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The header; a member that is missing or unreadable is <see langword="null"/>.</returns>
    public Task<PluginPackageHeader> ReadHeaderAsync(Stream package, CancellationToken ct);

    /// <summary>
    /// Reads the whole manifest out of an archive without unpacking it to disk, so the admin can see what a package asks
    /// for before it is installed. The manifest is not checked here: the install still runs every rule.
    /// </summary>
    /// <param name="package">A seekable ZIP stream; it is left open.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The manifest; <see langword="null"/> when the archive or its <c>plugin.json</c> is missing, oversized or unreadable.</returns>
    public Task<PluginManifest?> ReadManifestAsync(Stream package, CancellationToken ct);

    /// <summary>Tells whether a key id is one the operator listed under <c>PluginSigning:OperatorKeys</c>.</summary>
    /// <param name="keyId">The key id from a package signature.</param>
    /// <returns><see langword="true"/> for an operator key; the maintainers' shipped keys do not count.</returns>
    public bool IsOperatorKey(string keyId);
}
