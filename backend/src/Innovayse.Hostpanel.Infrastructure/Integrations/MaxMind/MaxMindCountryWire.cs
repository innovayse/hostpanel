namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>A country in a minFraud Insights answer.</summary>
/// <param name="IsoCode">The ISO 3166-1 alpha-2 code.</param>
internal sealed record MaxMindCountryWire(string? IsoCode);
