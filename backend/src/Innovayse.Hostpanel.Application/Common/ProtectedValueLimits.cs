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
    /// The longest value a setting row stores. A secret setting is encrypted before it is saved,
    /// so the column is sized for the encrypted form of this length. The figure is high enough for an operator's own
    /// legal text (<c>portal.legal.*</c>), the longest free text a setting holds.
    /// </summary>
    public const int SettingValue = 12000;
}
