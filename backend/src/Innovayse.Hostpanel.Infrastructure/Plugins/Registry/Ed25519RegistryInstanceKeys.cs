using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// <see cref="IRegistryInstanceKeys"/> over Ed25519 from the cryptography library the plugin package signature check already
/// uses, so no second library is needed. Ed25519 is deterministic: the same key and text always give the same signature.
/// </summary>
public sealed class Ed25519RegistryInstanceKeys : IRegistryInstanceKeys
{
    /// <summary>The length of an Ed25519 private seed in bytes.</summary>
    private const int SeedLength = 32;

    /// <inheritdoc/>
    public RegistryInstanceKeyPair Generate()
    {
        var seed = RandomNumberGenerator.GetBytes(SeedLength);
        var publicKey = new Ed25519PrivateKeyParameters(seed, 0).GeneratePublicKey().GetEncoded();
        return new RegistryInstanceKeyPair(Convert.ToBase64String(publicKey), Convert.ToBase64String(seed));
    }

    /// <inheritdoc/>
    /// <exception cref="FormatException">The private key is not base64.</exception>
    /// <exception cref="ArgumentException">The private key is not 32 bytes.</exception>
    public string Sign(string privateKey, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(privateKey);
        ArgumentNullException.ThrowIfNull(payload);

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, new Ed25519PrivateKeyParameters(Convert.FromBase64String(privateKey), 0));
        signer.BlockUpdate(payload, 0, payload.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }
}
