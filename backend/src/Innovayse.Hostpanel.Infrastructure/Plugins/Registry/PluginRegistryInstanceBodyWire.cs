namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The JSON body sent to the registry to register this installation.</summary>
/// <param name="PublicKey">The raw 32-byte Ed25519 public key, base64.</param>
/// <param name="BaseUrl">The public https address of this installation.</param>
internal sealed record PluginRegistryInstanceBodyWire(string PublicKey, string BaseUrl);
