namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The body the EU VAT number service is asked with.</summary>
/// <param name="CountryCode">The member state prefix of the number as the service spells it (Greece is <c>EL</c>).</param>
/// <param name="VatNumber">The number without its prefix.</param>
internal sealed record ViesLookupWire(string CountryCode, string VatNumber);
