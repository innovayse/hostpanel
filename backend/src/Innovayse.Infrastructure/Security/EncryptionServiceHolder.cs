namespace Innovayse.Infrastructure.Security;

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
