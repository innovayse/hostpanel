namespace Innovayse.Application.Domains.Common;

/// <summary>Pricing details for a single TLD extension.</summary>
/// <param name="Register">Registration prices keyed by period in years (e.g. "1" => "9.99").</param>
/// <param name="Transfer">Transfer prices keyed by period in years.</param>
/// <param name="Renew">Renewal prices keyed by period in years.</param>
/// <param name="Categories">Category tags for filtering (e.g. "Popular", "Country").</param>
public record TldPriceEntryDto(
    Dictionary<string, string> Register,
    Dictionary<string, string> Transfer,
    Dictionary<string, string> Renew,
    List<string> Categories);
