using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Common;

/// <summary>A VAT rate of an EU member state as staff see it.</summary>
/// <param name="Id">Row primary key.</param>
/// <param name="Country">The ISO 3166-1 alpha-2 country code of the member state, upper case.</param>
/// <param name="Kind">Whether the row holds the standard or a reduced rate.</param>
/// <param name="RatePercent">The rate in percent, from 0 to 100.</param>
/// <param name="ValidFrom">The first day (inclusive) the rate applies, <c>yyyy-MM-dd</c> on the wire.</param>
public sealed record VatRateDto(int Id, string Country, VatRateKind Kind, decimal RatePercent, DateOnly ValidFrom);
