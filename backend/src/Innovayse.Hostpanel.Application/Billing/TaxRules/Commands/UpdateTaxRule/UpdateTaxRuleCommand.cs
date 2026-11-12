namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.UpdateTaxRule;

/// <summary>Command to replace a tax rule's name, place, rate and switch.</summary>
/// <param name="Id">The rule primary key. Overlaid from the route.</param>
/// <param name="Name">The new display name.</param>
/// <param name="Country">The new ISO 3166-1 alpha-2 country code.</param>
/// <param name="Region">The new region, or null for the whole country.</param>
/// <param name="RatePercent">The new tax rate in percent, from 0 to 100.</param>
/// <param name="IsEnabled">Whether new invoices use the rule.</param>
public sealed record UpdateTaxRuleCommand(int Id, string Name, string Country, string? Region, decimal RatePercent, bool IsEnabled);
