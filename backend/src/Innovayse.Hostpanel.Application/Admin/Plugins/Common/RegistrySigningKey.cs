namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One signing key of a publisher, as the registry publishes it.</summary>
/// <param name="KeyId">The key id.</param>
/// <param name="PublicKey">The Ed25519 public key, base64 of its 32 bytes.</param>
/// <param name="Revoked">Whether the publisher revoked the key; a package signed with it is no longer trusted.</param>
/// <param name="Compromised">Whether the key was revoked because it leaked.</param>
public sealed record RegistrySigningKey(string KeyId, string PublicKey, bool Revoked, bool Compromised);
