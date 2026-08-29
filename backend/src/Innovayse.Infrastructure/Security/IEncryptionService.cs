namespace Innovayse.Infrastructure.Security;

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
