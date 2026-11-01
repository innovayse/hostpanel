using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Signs a plugin package. The host never signs anything: this is for whoever publishes a plugin, through the
/// <c>Innovayse.Hostpanel.PluginSigner</c> tool, and for the tests. The private key is held by the signer, outside
/// this repository, and is never written into a package.
/// </summary>
public static class PluginSigner
{
    /// <summary>The length of an Ed25519 private key (its seed) in bytes.</summary>
    public const int PrivateKeyLength = 32;

    /// <summary>Makes a new key pair.</summary>
    /// <returns>The 32-byte private key and the matching 32-byte public key.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        var privateKey = RandomNumberGenerator.GetBytes(PrivateKeyLength);
        return (privateKey, PublicKeyOf(privateKey));
    }

    /// <summary>Derives the public key of a private key.</summary>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <returns>The 32-byte public key.</returns>
    /// <exception cref="ArgumentException">The private key is not 32 bytes.</exception>
    public static byte[] PublicKeyOf(byte[] privateKey)
    {
        return PrivateParameters(privateKey).GeneratePublicKey().GetEncoded();
    }

    /// <summary>
    /// Signs the package in a folder and writes <c>plugin.sig</c> at its root, replacing an earlier one. Sign only
    /// after every other file is final: a change to any file afterwards makes the signature invalid.
    /// </summary>
    /// <param name="packageDir">The package root: the folder holding <c>plugin.json</c>.</param>
    /// <param name="keyId">The id the host's trusted key list knows this key by.</param>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <returns>The signature that was written.</returns>
    /// <exception cref="InvalidDataException">The key id is not valid, or a file name in the package cannot be signed.</exception>
    /// <exception cref="ArgumentException">The private key is not 32 bytes.</exception>
    public static PluginSignatureFile Sign(string packageDir, string keyId, byte[] privateKey)
    {
        var payload = PluginSignaturePayload.Build(packageDir, keyId);

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, PrivateParameters(privateKey));
        signer.BlockUpdate(payload, 0, payload.Length);

        var file = new PluginSignatureFile(keyId, signer.GenerateSignature());
        File.WriteAllText(Path.Combine(packageDir, PluginSignatureFile.FileName), file.ToJson());
        return file;
    }

    /// <summary>Wraps raw private key bytes for the Ed25519 implementation.</summary>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <returns>The key parameters.</returns>
    /// <exception cref="ArgumentException">The private key is not 32 bytes.</exception>
    private static Ed25519PrivateKeyParameters PrivateParameters(byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        if (privateKey.Length != PrivateKeyLength)
        {
            throw new ArgumentException($"An Ed25519 private key is {PrivateKeyLength} bytes.", nameof(privateKey));
        }

        return new Ed25519PrivateKeyParameters(privateKey, 0);
    }
}
