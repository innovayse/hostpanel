namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The member states of the European Union, by ISO 3166-1 alpha-2 code as client records store
/// them (Greece is <c>GR</c> here, though VAT returns write <c>EL</c>). The OSS report
/// covers only these countries, and the VAT rate table (<c>VatRate</c>) holds only these countries.
/// </summary>
public static class EuMemberStates
{
    /// <summary>The 27 member-state codes, in alphabetical order.</summary>
    public static readonly string[] Codes =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU",
        "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK",
    ];
}
