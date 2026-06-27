namespace Innovayse.Domain.Domains;

/// <summary>
/// Represents pricing information for a single TLD extension from the registrar.
/// </summary>
/// <param name="Tld">The TLD extension without the leading dot (e.g. "am", "com").</param>
/// <param name="Currency">ISO 4217 currency code (e.g. "USD", "AMD").</param>
/// <param name="Register">Registration prices keyed by period in years.</param>
/// <param name="Transfer">Transfer prices keyed by period in years.</param>
/// <param name="Renew">Renewal prices keyed by period in years.</param>
public record TldPricing(
    string Tld,
    string Currency,
    Dictionary<int, decimal> Register,
    Dictionary<int, decimal> Transfer,
    Dictionary<int, decimal> Renew);
