namespace Innovayse.Infrastructure.Security;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// EF Core value converter that transparently encrypts strings when writing to the database
/// and decrypts them when reading. Null values pass through unchanged.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    /// <summary>
    /// Initialises the converter with the specified encryption service.
    /// </summary>
    /// <param name="encryption">The encryption service to use for encrypt/decrypt operations.</param>
    public EncryptedStringConverter(IEncryptionService encryption)
        : base(
            v => v != null ? encryption.Encrypt(v) : null,
            v => v != null ? encryption.Decrypt(v) : null)
    {
    }
}

/// <summary>
/// Static holder for the encryption service instance, set during app startup.
/// Used by EF Core configurations which do not have access to DI.
/// </summary>
public static class EncryptionServiceHolder
{
    /// <summary>Gets or sets the global encryption service instance.</summary>
    public static IEncryptionService? Instance { get; set; }

    /// <summary>
    /// Creates an <see cref="EncryptedStringConverter"/> using the global <see cref="Instance"/>.
    /// </summary>
    /// <returns>A new converter, or <c>null</c> if encryption is not configured.</returns>
    public static EncryptedStringConverter? CreateConverter() =>
        Instance is not null ? new EncryptedStringConverter(Instance) : null;
}
