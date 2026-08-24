namespace Innovayse.Providers.Inecobank;

/// <summary>
/// Field keys for the Inecobank plugin's admin-configured settings, matching the <c>key</c>
/// values declared in <c>plugin.json</c> (which stays literal — it is data read by the plugin
/// loader, not compiled C#). Every C# call site that reads one of these fields should use the
/// constant here instead of re-typing the string.
/// </summary>
internal static class InecobankConfigKeys
{
    /// <summary>Key for the gateway's base URL.</summary>
    public const string GatewayUrl = "gateway_url";

    /// <summary>Key for the merchant API username.</summary>
    public const string Username = "username";

    /// <summary>Key for the merchant API password.</summary>
    public const string Password = "password";

    /// <summary>Key for the ISO 4217 numeric currency code.</summary>
    public const string Currency = "currency";

    /// <summary>Key for the ISO 639-1 payment page language.</summary>
    public const string Language = "language";
}
