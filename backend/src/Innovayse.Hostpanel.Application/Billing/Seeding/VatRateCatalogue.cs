namespace Innovayse.Hostpanel.Application.Billing.Seeding;

/// <summary>
/// The standard VAT rate of each EU member state that ships with the application, with the day each rate applies from.
/// A rate change is a new entry with a later day; an entry already shipped is never edited, because an installation that
/// imported it would keep the old rate. <see cref="VatRateSeeder"/> adds whatever an installation lacks at startup, so
/// installing a release that carries a changed rate brings the change. Only the standard rate is shipped: a reduced rate
/// depends on what is sold, so staff enter those on the VAT rates screen.
/// </summary>
/// <remarks>
/// Rates are the statutory standard rates as published by each member state. Check them against the European
/// Commission's VAT rates publication before a release that adds or changes an entry.
/// </remarks>
public static class VatRateCatalogue
{
    /// <summary>The day the first shipped rates apply from; an invoice older than this has no destination rate.</summary>
    private static readonly DateOnly Baseline = new(2024, 1, 1);

    /// <summary>Every shipped rate: the rate in force on <see cref="Baseline"/> for each of the 27 member states, then each change since.</summary>
    public static readonly IReadOnlyList<ShippedVatRate> Rates =
    [
        new("AT", 20m, Baseline),
        new("BE", 21m, Baseline),
        new("BG", 20m, Baseline),
        new("CY", 19m, Baseline),
        new("CZ", 21m, Baseline),
        new("DE", 19m, Baseline),
        new("DK", 25m, Baseline),
        new("EE", 22m, Baseline),
        new("ES", 21m, Baseline),
        new("FI", 24m, Baseline),
        new("FR", 20m, Baseline),
        new("GR", 24m, Baseline),
        new("HR", 25m, Baseline),
        new("HU", 27m, Baseline),
        new("IE", 23m, Baseline),
        new("IT", 22m, Baseline),
        new("LT", 21m, Baseline),
        new("LU", 17m, Baseline),
        new("LV", 21m, Baseline),
        new("MT", 18m, Baseline),
        new("NL", 21m, Baseline),
        new("PL", 23m, Baseline),
        new("PT", 23m, Baseline),
        new("RO", 19m, Baseline),
        new("SE", 25m, Baseline),
        new("SI", 22m, Baseline),
        new("SK", 20m, Baseline),
        new("FI", 25.5m, new DateOnly(2024, 9, 1)),
        new("SK", 23m, new DateOnly(2025, 1, 1)),
        new("EE", 24m, new DateOnly(2025, 7, 1)),
        new("RO", 21m, new DateOnly(2025, 8, 1)),
    ];
}
