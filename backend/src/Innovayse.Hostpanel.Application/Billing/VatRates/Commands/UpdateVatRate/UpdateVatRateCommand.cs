using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.UpdateVatRate;

/// <summary>Command to replace a VAT rate row's country, kind, rate and first day.</summary>
/// <param name="Id">The row primary key. Overlaid from the route.</param>
/// <param name="Country">The new ISO 3166-1 alpha-2 country code of an EU member state.</param>
/// <param name="Kind">The new kind of rate.</param>
/// <param name="RatePercent">The new rate in percent, from 0 to 100.</param>
/// <param name="ValidFrom">The new first day the rate applies.</param>
public sealed record UpdateVatRateCommand(int Id, string Country, VatRateKind Kind, decimal RatePercent, DateOnly ValidFrom);
