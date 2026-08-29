namespace Innovayse.Application.Domains.Common;

/// <summary>Currency metadata for TLD pricing.</summary>
/// <param name="Code">ISO 4217 currency code (e.g. "USD").</param>
/// <param name="Prefix">Currency symbol prefix (e.g. "$").</param>
public record TldCurrencyDto(string Code, string Prefix);
