namespace Innovayse.Application.Billing.Currencies.Queries.ListCurrencies;

/// <summary>A configured currency as the admin currencies page sees it, rate included.</summary>
/// <param name="Code">ISO 4217 alpha code, upper case.</param>
/// <param name="Numeric">ISO 4217 numeric code, three digits.</param>
/// <param name="Prefix">Text printed before a formatted amount, e.g. "$".</param>
/// <param name="Suffix">Text printed after a formatted amount, e.g. " ֏".</param>
/// <param name="Decimals">Decimal places an amount is rounded and shown to.</param>
/// <param name="RateToBase">How much of the base one unit of this currency is worth.</param>
/// <param name="IsBase">Whether this is the base currency; exactly one row is.</param>
/// <param name="IsEnabled">Whether new clients may still choose it.</param>
public record CurrencyDto(
    string Code,
    string Numeric,
    string Prefix,
    string Suffix,
    int Decimals,
    decimal RateToBase,
    bool IsBase,
    bool IsEnabled);
