namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The slugs of the built-in integrations. A slug is the persisted key segment in
/// <see cref="IntegrationSettingKeys"/> and the identifier the admin panel addresses an
/// integration by, so the values never change.
/// </summary>
public static class IntegrationSlugs
{
    /// <summary>The manual bank-transfer method.</summary>
    public const string BankTransfer = "bank-transfer";

    /// <summary>The cPanel WHM provisioning module.</summary>
    public const string Cpanel = "cpanel";

    /// <summary>The SMTP mail server.</summary>
    public const string Smtp = "smtp";

    /// <summary>The MaxMind fraud-protection service.</summary>
    public const string MaxMind = "maxmind";
}
