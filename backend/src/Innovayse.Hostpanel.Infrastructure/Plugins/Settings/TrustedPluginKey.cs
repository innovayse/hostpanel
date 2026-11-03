namespace Innovayse.Hostpanel.Infrastructure.Plugins.Settings;

/// <summary>One public key a plugin package signature is checked against. Never a private key.</summary>
public sealed class TrustedPluginKey
{
    /// <summary>The length of an Ed25519 public key in bytes.</summary>
    public const int PublicKeyLength = 32;

    /// <summary>The id a package's <c>plugin.sig</c> names to pick this key; see <see cref="TrustedKeyId"/> for its shape.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>The base64 of the 32-byte raw Ed25519 public key.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Decodes <see cref="PublicKey"/>.</summary>
    /// <param name="key">The 32 key bytes, when the text is a well-formed key.</param>
    /// <returns><see langword="true"/> when <see cref="PublicKey"/> is base64 of exactly 32 bytes.</returns>
    public bool TryDecode(out byte[] key)
    {
        key = [];
        var buffer = new byte[PublicKeyLength];
        if (!Convert.TryFromBase64String(PublicKey, buffer, out var written) || written != PublicKeyLength)
        {
            return false;
        }

        key = buffer;
        return true;
    }
}
