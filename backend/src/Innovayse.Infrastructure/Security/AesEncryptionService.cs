namespace Innovayse.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Provides AES-256-CBC encryption and decryption for sensitive strings.
/// A random IV is generated per encryption and prepended to the ciphertext.
/// The result is Base64-encoded for safe database storage.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypts a plaintext string.</summary>
    /// <param name="plaintext">The string to encrypt.</param>
    /// <returns>Base64-encoded ciphertext with prepended IV.</returns>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a Base64-encoded ciphertext.</summary>
    /// <param name="ciphertext">The Base64-encoded string to decrypt.</param>
    /// <returns>The original plaintext.</returns>
    string Decrypt(string ciphertext);
}

/// <summary>
/// AES-256-CBC implementation of <see cref="IEncryptionService"/>.
/// Uses a 32-byte key from configuration and a random 16-byte IV per encryption.
/// Legacy plaintext values that cannot be decrypted are returned as-is for backward compatibility.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    /// <summary>The 32-byte AES-256 key.</summary>
    private readonly byte[] _key;

    /// <summary>
    /// Initialises the service with a Base64-encoded 32-byte key.
    /// </summary>
    /// <param name="base64Key">Base64-encoded encryption key (must decode to exactly 32 bytes).</param>
    /// <exception cref="ArgumentException">Thrown when the key is not exactly 32 bytes.</exception>
    public AesEncryptionService(string base64Key)
    {
        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
            throw new ArgumentException("Encryption key must be exactly 32 bytes (256 bits).", nameof(base64Key));
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext: [16-byte IV][ciphertext]
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        try
        {
            var fullBytes = Convert.FromBase64String(ciphertext);

            // IV is 16 bytes; ciphertext must be at least one AES block (16 bytes)
            if (fullBytes.Length < 17)
                return ciphertext; // Too short to be encrypted — legacy plaintext

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract IV (first 16 bytes) and ciphertext (remainder)
            var iv = fullBytes[..16];
            var cipher = fullBytes[16..];

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (FormatException)
        {
            // Not valid Base64 — legacy plaintext data
            return ciphertext;
        }
        catch (CryptographicException)
        {
            // Decryption failed — legacy plaintext data
            return ciphertext;
        }
    }
}
