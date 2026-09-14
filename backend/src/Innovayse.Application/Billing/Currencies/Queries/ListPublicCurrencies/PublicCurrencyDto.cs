namespace Innovayse.Application.Billing.Currencies.Queries.ListPublicCurrencies;

/// <summary>A currency as the storefront sees it: enough to offer and format it, and no rate.</summary>
/// <remarks>
/// The rate is deliberately absent. A visitor is shown stored prices, never converted ones, so
/// the rate would only invite the storefront to do arithmetic the backend refuses to do.
/// </remarks>
/// <param name="Code">ISO 4217 alpha code, upper case.</param>
/// <param name="Numeric">ISO 4217 numeric code, three digits.</param>
/// <param name="Prefix">Text printed before a formatted amount, e.g. "$".</param>
/// <param name="Suffix">Text printed after a formatted amount, e.g. " ֏".</param>
/// <param name="Decimals">Decimal places an amount is shown to.</param>
/// <param name="IsBase">Whether this is the base currency, which a checkout offers by default.</param>
public record PublicCurrencyDto(
    string Code,
    string Numeric,
    string Prefix,
    string Suffix,
    int Decimals,
    bool IsBase);
