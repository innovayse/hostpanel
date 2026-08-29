namespace Innovayse.Application.Admin.Common;

/// <summary>
/// DTO representing a currency with its ISO 4217 code, name, and display symbol.
/// </summary>
/// <param name="Code">ISO 4217 three-letter currency code (e.g. "USD").</param>
/// <param name="Name">Human-readable currency name (e.g. "US Dollar").</param>
/// <param name="Symbol">Display symbol used for formatting (e.g. "$").</param>
public record CurrencyDto(string Code, string Name, string Symbol);
