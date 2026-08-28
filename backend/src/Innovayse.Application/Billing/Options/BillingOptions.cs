namespace Innovayse.Application.Billing.Options;

/// <summary>
/// Configuration options for the panel-wide billing defaults.
/// Bound from the "Billing" section in appsettings.
/// </summary>
public sealed class BillingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Billing";

    /// <summary>
    /// The ISO 4217 alpha code (three letters, e.g. <c>AMD</c>, <c>USD</c>) a client is billed in
    /// when the client record itself names no currency.
    /// </summary>
    /// <remarks>
    /// <c>Client.Create</c> never sets a currency, so this covers every existing client row. The
    /// default is AMD rather than an internationally "neutral" code like USD because Armenian
    /// merchants are this panel's only production gateway integration to date; a deployment
    /// billing anywhere else overrides it.
    /// </remarks>
    public string DefaultCurrency { get; set; } = "AMD";
}
