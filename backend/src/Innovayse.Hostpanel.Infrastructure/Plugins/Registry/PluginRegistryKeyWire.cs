namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry's JSON for one signing key of a publisher.</summary>
/// <param name="KeyId">The key id.</param>
/// <param name="PublicKey">The Ed25519 public key, base64 of its 32 bytes.</param>
/// <param name="Revoked">Whether the publisher revoked the key; a missing value reads as not revoked.</param>
/// <param name="Compromised">Whether the key was revoked because it leaked; a missing value reads as not compromised.</param>
internal sealed record PluginRegistryKeyWire(string? KeyId, string? PublicKey, bool? Revoked, bool? Compromised);
