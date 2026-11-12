namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Common;

/// <summary>A tax rule as staff see it.</summary>
/// <param name="Id">Rule primary key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Country">The ISO 3166-1 alpha-2 country code, upper case.</param>
/// <param name="Region">The region the rule is limited to, or null for a country-wide rule.</param>
/// <param name="RatePercent">The tax rate in percent, from 0 to 100.</param>
/// <param name="IsEnabled">Whether new invoices use the rule.</param>
public sealed record TaxRuleDto(int Id, string Name, string Country, string? Region, decimal RatePercent, bool IsEnabled);
