using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// The countries whose VAT numbers can be checked, and the shape a number is sent in. The registers are the EU member
/// states' (through one EU service) and the United Kingdom's (through its own); a number from any other country has no
/// register to ask, so it is never checked.
/// </summary>
public static class VatRegisters
{
    /// <summary>The code of the United Kingdom, whose numbers are not in the EU register.</summary>
    public const string UnitedKingdom = "GB";

    /// <summary>The ISO code of Greece, as client records store it; its VAT numbers are prefixed <see cref="GreekVatPrefix"/>.</summary>
    private const string Greece = "GR";

    /// <summary>The prefix VAT numbers of Greece carry, which differs from its ISO 3166-1 code.</summary>
    private const string GreekVatPrefix = "EL";

    /// <summary>The countries a number can be checked for: the EU member states and the United Kingdom.</summary>
    private static readonly HashSet<string> Covered = new([.. EuMemberStates.Codes, UnitedKingdom], StringComparer.Ordinal);

    /// <summary>Gets the codes of the countries a number can be checked for, for a query that selects the clients to check.</summary>
    public static IReadOnlyCollection<string> Countries => Covered;

    /// <summary>Whether a register exists for a country's VAT numbers.</summary>
    /// <param name="country">An ISO 3166-1 alpha-2 code as client records store it, or null.</param>
    /// <returns><see langword="true"/> for an EU member state or the United Kingdom.</returns>
    public static bool Covers(string? country) => country is not null && Covered.Contains(country.Trim().ToUpperInvariant());

    /// <summary>The prefix the VAT numbers of a country carry: its ISO 3166-1 code, except for Greece (<c>EL</c>).</summary>
    /// <param name="country">The ISO 3166-1 alpha-2 code of a country <see cref="Covers"/> accepts, upper case.</param>
    /// <returns>The two-letter prefix.</returns>
    public static string PrefixOf(string country) => country == Greece ? GreekVatPrefix : country;

    /// <summary>Puts a client's tax identification number in the shape the registers take.</summary>
    /// <remarks>
    /// Spaces and punctuation go, the letters go upper case, and the country prefix the client may have typed (<c>DE</c>, or
    /// <c>EL</c> for Greece) is cut, so <c>de 123.456.789</c> and <c>123456789</c> are the same lookup.
    /// </remarks>
    /// <param name="country">The ISO 3166-1 alpha-2 code of a country <see cref="Covers"/> accepts.</param>
    /// <param name="taxId">The number as the client holds it.</param>
    /// <returns>The lookup; its number is empty when nothing but a prefix was typed.</returns>
    public static VatNumberLookup LookupFor(string country, string taxId)
    {
        var upperCountry = country.Trim().ToUpperInvariant();
        var number = new string(taxId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        // A Greek number is written with EL on VAT documents and with the ISO code GR by people who go by the country.
        string[] prefixes = [PrefixOf(upperCountry), upperCountry];
        var prefix = prefixes.FirstOrDefault(p => number.StartsWith(p, StringComparison.Ordinal));
        if (prefix is not null)
        {
            number = number[prefix.Length..];
        }

        return new VatNumberLookup(upperCountry, number);
    }
}
