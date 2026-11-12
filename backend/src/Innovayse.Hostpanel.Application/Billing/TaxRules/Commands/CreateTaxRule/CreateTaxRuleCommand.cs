namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.CreateTaxRule;

/// <summary>Command to create a tax rule for a country, or for one region of it.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Country">The ISO 3166-1 alpha-2 country code.</param>
/// <param name="Region">The region the rule is limited to, or null for the whole country.</param>
/// <param name="RatePercent">The tax rate in percent, from 0 to 100.</param>
/// <param name="IsEnabled">Whether new invoices use the rule.</param>
public sealed record CreateTaxRuleCommand(string Name, string Country, string? Region, decimal RatePercent, bool IsEnabled);
