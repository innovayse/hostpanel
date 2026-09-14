namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for creating or rewriting a currency; the code is the route segment.</summary>
/// <param name="Numeric">ISO 4217 numeric code, three digits; read only when the row is created.</param>
/// <param name="Prefix">Text printed before a formatted amount, e.g. "$".</param>
/// <param name="Suffix">Text printed after a formatted amount, e.g. " ֏".</param>
/// <param name="Decimals">Decimal places an amount is rounded and shown to.</param>
/// <param name="RateToBase">How much of the base one unit of this currency is worth; ignored for the base.</param>
/// <param name="IsEnabled">Whether new clients may choose it. The base cannot be switched off.</param>
public record UpsertCurrencyRequest(
    string Numeric,
    string Prefix,
    string Suffix,
    int Decimals,
    decimal RateToBase,
    bool IsEnabled);
