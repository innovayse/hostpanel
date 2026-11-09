namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Encrypts the value of a secret setting before it is stored and decrypts it when a caller
/// needs the plain value (a payment plugin's key, a registrar password, the setup token).
/// Which keys are secret is decided by <c>SettingSecrecy</c>; this port only transforms values.
/// </summary>
/// <remarks>
/// A protected value carries a fixed marker, so <see cref="IsProtected"/> tells an encrypted row
/// from a plain one stored by an earlier release. <see cref="Reveal"/> returns a plain value
/// unchanged, which keeps such a row readable until the startup step re-encrypts it.
/// </remarks>
public interface ISecretSettingCipher
{
    /// <summary>Encrypts a secret value for storage. An empty value stays empty.</summary>
    /// <param name="plaintext">The value the operator entered.</param>
    /// <returns>The value to store.</returns>
    public string Protect(string plaintext);

    /// <summary>Returns the plain value of a stored secret.</summary>
    /// <param name="stored">The stored value, protected or a plain value from an earlier release.</param>
    /// <returns>The plain value.</returns>
    public string Reveal(string stored);

    /// <summary>Tells whether a stored value is already protected.</summary>
    /// <param name="stored">The stored value.</param>
    /// <returns><c>true</c> when the value carries the protection marker.</returns>
    public bool IsProtected(string stored);
}
