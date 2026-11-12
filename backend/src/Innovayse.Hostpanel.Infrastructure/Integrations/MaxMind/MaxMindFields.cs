namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>The field names the MaxMind integration stores, as the integration catalogue declares them.</summary>
public static class MaxMindFields
{
    /// <summary>The MaxMind account id.</summary>
    public const string AccountId = "account_id";

    /// <summary>The MaxMind license key; a secret setting, stored encrypted.</summary>
    public const string LicenseKey = "license_key";
}
