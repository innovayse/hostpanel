using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.CreateVatRate;

/// <summary>Command to add the VAT rate of an EU member state from a given day on.</summary>
/// <param name="Country">The ISO 3166-1 alpha-2 country code of an EU member state.</param>
/// <param name="Kind">Whether the row holds the standard or a reduced rate.</param>
/// <param name="RatePercent">The rate in percent, from 0 to 100.</param>
/// <param name="ValidFrom">The first day the rate applies.</param>
public sealed record CreateVatRateCommand(string Country, VatRateKind Kind, decimal RatePercent, DateOnly ValidFrom);
