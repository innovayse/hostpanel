using Innovayse.Hostpanel.Domain.Plugins.Enums;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>What the host knows about one plugin package it accepted, as recorded on an <see cref="InstalledPlugin"/>.</summary>
/// <param name="Source">Where the package came from; never <see cref="PluginSource.BuiltIn"/>.</param>
/// <param name="Signer">Who vouched for the package with a signature.</param>
/// <param name="SignerKeyId">The id of the key that signed the package; <see langword="null"/> exactly when <paramref name="Signer"/> is <see cref="PluginSignerKind.Unsigned"/>.</param>
/// <param name="SizeBytes">The size of the archive in bytes, positive.</param>
/// <param name="Sha256">The lower-case hex SHA-256 of the archive.</param>
/// <param name="SignerPublicKey">The public key the registry published for the signing key, kept only for a registry install of a key the host did not trust itself; <see langword="null"/> otherwise.</param>
public sealed record PluginPackageFacts(PluginSource Source, PluginSignerKind Signer, string? SignerKeyId, long SizeBytes, string Sha256, string? SignerPublicKey = null);
