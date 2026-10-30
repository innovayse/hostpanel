using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// Reads the billing-cycle texts older data and external sources carry into a <see cref="BillingCycle"/>.
/// </summary>
/// <remarks>
/// Three spellings were in the wild before the cycle became an enum: "monthly", "annual" and
/// "annually". The cycles added later are read by their member name, in any casing, and "semi-annual"
/// is accepted beside "semiannual" because importers and people write both. They are read here, in
/// one place, by the persistence converter and by the importers; nothing else in the code works with
/// a cycle as text.
/// </remarks>
public static class BillingCycleParser
{
    /// <summary>The legacy spelling of <see cref="BillingCycle.Monthly"/>.</summary>
    private const string Monthly = "monthly";

    /// <summary>The legacy spelling of <see cref="BillingCycle.Annual"/>.</summary>
    private const string Annual = "annual";

    /// <summary>The alternative legacy spelling of <see cref="BillingCycle.Annual"/>.</summary>
    private const string Annually = "annually";

    /// <summary>The spelling of <see cref="BillingCycle.Quarterly"/>.</summary>
    private const string Quarterly = "quarterly";

    /// <summary>The spelling of <see cref="BillingCycle.SemiAnnual"/>.</summary>
    private const string SemiAnnual = "semiannual";

    /// <summary>The hyphenated spelling of <see cref="BillingCycle.SemiAnnual"/>.</summary>
    private const string SemiAnnualHyphenated = "semi-annual";

    /// <summary>The spelling of <see cref="BillingCycle.Biennial"/>.</summary>
    private const string Biennial = "biennial";

    /// <summary>Reads a stored or imported billing-cycle text without throwing.</summary>
    /// <param name="raw">The text, any casing and surrounding blanks; may be null.</param>
    /// <param name="cycle">The cycle when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="raw"/> names a known cycle.</returns>
    public static bool TryParse(string? raw, out BillingCycle cycle)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case Monthly:
                cycle = BillingCycle.Monthly;
                return true;
            case Annual or Annually:
                cycle = BillingCycle.Annual;
                return true;
            case Quarterly:
                cycle = BillingCycle.Quarterly;
                return true;
            case SemiAnnual or SemiAnnualHyphenated:
                cycle = BillingCycle.SemiAnnual;
                return true;
            case Biennial:
                cycle = BillingCycle.Biennial;
                return true;
            default:
                cycle = default;
                return false;
        }
    }
}
