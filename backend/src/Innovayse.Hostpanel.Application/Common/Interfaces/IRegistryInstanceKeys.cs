using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The Ed25519 operations of this installation's registry identity. Kept behind a port because the cryptography library
/// lives in Infrastructure; Application only needs a pair to be made and a text to be signed.
/// </summary>
public interface IRegistryInstanceKeys
{
    /// <summary>Generates a new key pair.</summary>
    /// <returns>The pair, both halves as base64 of the raw 32 bytes.</returns>
    public RegistryInstanceKeyPair Generate();

    /// <summary>Signs a text with a private key.</summary>
    /// <param name="privateKey">The private seed, base64 of the raw 32 bytes.</param>
    /// <param name="payload">The bytes to sign.</param>
    /// <returns>The 64-byte signature, base64.</returns>
    public string Sign(string privateKey, byte[] payload);
}
