namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>A public key a package signature is checked against, together with the id the package's <c>plugin.sig</c> names it by.</summary>
/// <param name="KeyId">The key id.</param>
/// <param name="PublicKey">The Ed25519 public key, base64 of its 32 bytes.</param>
public sealed record PluginSignerKey(string KeyId, string PublicKey);
