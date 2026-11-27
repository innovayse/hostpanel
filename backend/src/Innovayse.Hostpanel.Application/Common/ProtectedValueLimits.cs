namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// The longest plain value each protected field accepts, in characters. The validators enforce
/// these figures and Infrastructure sizes the encrypted column from the same constants, so the
/// largest input a validator lets through always fits the column once it is encrypted.
/// </summary>
public static class ProtectedValueLimits
{
    /// <summary>The longest password stored for a server's API user.</summary>
    public const int ServerPassword = 500;

    /// <summary>The longest API token stored for a server.</summary>
    public const int ServerApiToken = 1000;

    /// <summary>The longest access hash stored for a server.</summary>
    public const int ServerAccessHash = 2000;

    /// <summary>The longest private key stored for a server's SSH agent.</summary>
    public const int AgentPrivateKey = 8000;

    /// <summary>The longest password stored for a hosting account.</summary>
    public const int ServicePassword = 256;

    /// <summary>The longest TOTP secret stored for a user, pending or active.</summary>
    public const int TwoFactorSecret = 128;

    /// <summary>
    /// The longest stored form of the registry's extra values of a domain or of an order line, the JSON text of the values by
    /// field key.
    /// </summary>
    public const int DomainExtendedAttributes = Domain.Domains.DomainExtendedAttributes.MaxSerializedLength;

    /// <summary>
    /// The longest value an ordinary setting row or integration value stores. A secret setting is
    /// encrypted before it is saved, so the column is sized for the encrypted form of
    /// <see cref="LegalTextValue"/>, the longest of the limits a setting row can carry.
    /// </summary>
    public const int SettingValue = 2000;

    /// <summary>
    /// The longest value one operator legal text (<c>portal.legal.*</c>) stores: a full terms of service in
    /// one language. Only those keys may exceed <see cref="SettingValue"/>.
    /// </summary>
    public const int LegalTextValue = 12000;
}
