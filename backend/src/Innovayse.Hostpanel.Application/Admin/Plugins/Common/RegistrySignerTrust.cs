using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Decides which public key vouches for a package fetched from the plugin registry. A key the host trusts from its own
/// configuration needs nothing more. Any other key is trusted only when the registry publishes it for the plugin's
/// publisher, names it on the release, the package's <c>plugin.sig</c> names the same key, and the publisher has not revoked
/// it. The answer is made for one install: the key is kept with the install record, and a revocation is seen at the next
/// install or update, when the registry is asked again.
/// </summary>
public sealed class RegistrySignerTrust
{
    /// <summary>The length of an Ed25519 public key in bytes.</summary>
    private const int PublicKeyLength = 32;

    /// <summary>The plugin registry the publisher's keys are read from.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Tells whether a key is one the host's configuration already trusts.</summary>
    private readonly IPluginPackageInspector _inspector;

    /// <summary>Initializes the decision.</summary>
    /// <param name="registry">The plugin registry the publisher's keys are read from.</param>
    /// <param name="inspector">Tells whether a key is one the host's configuration already trusts.</param>
    public RegistrySignerTrust(IPluginRegistryClient registry, IPluginPackageInspector inspector)
    {
        _registry = registry;
        _inspector = inspector;
    }

    /// <summary>Finds the key the registry vouches for the package's signer with.</summary>
    /// <param name="release">The release, which names the key the registry says signed it.</param>
    /// <param name="publisherHandle">The publisher's handle from the plugin page.</param>
    /// <param name="packageKeyId">The key id in the package's <c>plugin.sig</c>; <see langword="null"/> for an unsigned package.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful result holding the key to trust for this package, or <see langword="null"/> when the registry vouches for
    /// none and the host's configured keys decide alone (the store then refuses an unsigned package or an unknown key).
    /// <c>PLUGIN_REGISTRY_NOT_TRUSTED</c> when the publisher revoked the key, or the failure of the registry call.
    /// </returns>
    public async Task<Result<PluginSignerKey?>> ResolveAsync(
        RegistryPluginRelease release, string? publisherHandle, string? packageKeyId, CancellationToken ct)
    {
        if (packageKeyId is null || _inspector.IsConfiguredKey(packageKeyId))
        {
            return Result<PluginSignerKey?>.Success(null);
        }

        // The registry must stand behind the very key the package names, for this release and this publisher; when it does
        // not, nothing is added and the package meets the configured keys alone.
        if (publisherHandle is null || !string.Equals(release.KeyId, packageKeyId, StringComparison.Ordinal))
        {
            return Result<PluginSignerKey?>.Success(null);
        }

        var keys = await _registry.GetPublisherKeysAsync(publisherHandle, ct);
        if (keys.IsFailure)
        {
            return keys.ErrorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND
                ? Result<PluginSignerKey?>.Success(null)
                : Result<PluginSignerKey?>.FailureCode(keys.ErrorCode!);
        }

        var published = keys.Value!.FirstOrDefault(key => string.Equals(key.KeyId, packageKeyId, StringComparison.Ordinal));
        if (published is null || !IsPublicKey(published.PublicKey))
        {
            return Result<PluginSignerKey?>.Success(null);
        }

        // A revoked key is one the publisher no longer stands behind, even though the package is signed with it.
        return published.Revoked || published.Compromised
            ? Result<PluginSignerKey?>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_NOT_TRUSTED)
            : Result<PluginSignerKey?>.Success(new PluginSignerKey(published.KeyId, published.PublicKey));
    }

    /// <summary>Tells whether a text is the base64 of a 32-byte public key.</summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> for a well formed key.</returns>
    private static bool IsPublicKey(string value)
    {
        var buffer = new byte[PublicKeyLength];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == PublicKeyLength;
    }
}
