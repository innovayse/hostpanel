namespace Innovayse.Application.Common;

/// <summary>
/// DTO representing a currency with its ISO 4217 code, name, and display symbol.
/// </summary>
/// <param name="Code">ISO 4217 three-letter currency code (e.g. "USD").</param>
/// <param name="Name">Human-readable currency name (e.g. "US Dollar").</param>
/// <param name="Symbol">Display symbol used for formatting (e.g. "$").</param>
public record CurrencyDto(string Code, string Name, string Symbol);

/// <summary>
/// Static reference list of commonly used currencies.
/// Used by the admin UI for currency selection dropdowns.
/// </summary>
public static class CurrencyList
{
    /// <summary>All supported currencies, sorted by code.</summary>
    public static readonly IReadOnlyList<CurrencyDto> All =
    [
        new("AED", "UAE Dirham", "\u062f.\u0625"),
        new("AMD", "Armenian Dram", "\u058f"),
        new("AUD", "Australian Dollar", "A$"),
        new("BRL", "Brazilian Real", "R$"),
        new("CAD", "Canadian Dollar", "CA$"),
        new("CHF", "Swiss Franc", "CHF"),
        new("CNY", "Chinese Yuan", "\u00a5"),
        new("CZK", "Czech Koruna", "K\u010d"),
        new("DKK", "Danish Krone", "kr"),
        new("EUR", "Euro", "\u20ac"),
        new("GBP", "British Pound", "\u00a3"),
        new("GEL", "Georgian Lari", "\u20be"),
        new("HKD", "Hong Kong Dollar", "HK$"),
        new("HUF", "Hungarian Forint", "Ft"),
        new("INR", "Indian Rupee", "\u20b9"),
        new("JPY", "Japanese Yen", "\u00a5"),
        new("KRW", "South Korean Won", "\u20a9"),
        new("KZT", "Kazakhstani Tenge", "\u20b8"),
        new("MXN", "Mexican Peso", "MX$"),
        new("NOK", "Norwegian Krone", "kr"),
        new("NZD", "New Zealand Dollar", "NZ$"),
        new("PLN", "Polish Zloty", "z\u0142"),
        new("RUB", "Russian Ruble", "\u20bd"),
        new("SAR", "Saudi Riyal", "\ufdfc"),
        new("SEK", "Swedish Krona", "kr"),
        new("SGD", "Singapore Dollar", "S$"),
        new("TRY", "Turkish Lira", "\u20ba"),
        new("UAH", "Ukrainian Hryvnia", "\u20b4"),
        new("USD", "US Dollar", "$"),
        new("ZAR", "South African Rand", "R"),
    ];
}
