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

    /// <summary>The Namecheap registrar.</summary>
    public const string Namecheap = "namecheap";

    /// <summary>The Name.am registrar.</summary>
    public const string NameAm = "nameam";

    /// <summary>The ResellerClub registrar.</summary>
    public const string ResellerClub = "resellerclub";

    /// <summary>The ENOM registrar.</summary>
    public const string Enom = "enom";

    /// <summary>The cPanel WHM provisioning module.</summary>
    public const string Cpanel = "cpanel";

    /// <summary>The Plesk provisioning module.</summary>
    public const string Plesk = "plesk";

    /// <summary>The CentOS Web Panel provisioning module.</summary>
    public const string Cwp = "cwp";

    /// <summary>The CentOS Web Panel 7 provisioning module.</summary>
    public const string Cwp7 = "cwp7";

    /// <summary>The plugin id the hosting-panel server-info read side keys its CentOS Web Panel settings by.</summary>
    public const string InnovayseCwp = "innovayse-cwp";

    /// <summary>The SMTP mail server.</summary>
    public const string Smtp = "smtp";

    /// <summary>The MaxMind fraud-protection service.</summary>
    public const string MaxMind = "maxmind";
}
