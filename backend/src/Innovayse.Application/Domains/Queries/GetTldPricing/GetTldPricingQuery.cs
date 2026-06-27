namespace Innovayse.Application.Domains.Queries.GetTldPricing;

/// <summary>Query to retrieve TLD pricing information for domain registration, transfer, and renewal.</summary>
/// <param name="TargetCurrency">
/// ISO 4217 currency code to display prices in (e.g. "USD", "RUB", "AMD").
/// When null or empty, prices are returned in the registrar's native currency.
/// </param>
public record GetTldPricingQuery(string? TargetCurrency = null);
