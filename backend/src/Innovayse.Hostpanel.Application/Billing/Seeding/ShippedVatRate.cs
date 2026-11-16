namespace Innovayse.Hostpanel.Application.Billing.Seeding;

/// <summary>One standard VAT rate that ships with the application; see <see cref="VatRateCatalogue"/>.</summary>
/// <param name="Country">The ISO 3166-1 alpha-2 code of the member state, as client records store it.</param>
/// <param name="RatePercent">The standard rate in percent.</param>
/// <param name="ValidFrom">The first day the rate applies.</param>
public sealed record ShippedVatRate(string Country, decimal RatePercent, DateOnly ValidFrom);
