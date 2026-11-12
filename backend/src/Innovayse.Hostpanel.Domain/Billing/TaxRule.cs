using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A tax rate staff set for a country, or for one region (state) of a country. A new invoice takes the rate of the
/// enabled rule that matches the invoiced client's address: the region rule first, then the country-wide rule.
/// At most one rule exists per country and region.
/// Stored in the <c>TaxRules</c> table.
/// </summary>
public sealed class TaxRule : BaseEntity, IAggregateRoot
{
    /// <summary>Longest rule name the stored column holds.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Length of an ISO 3166-1 alpha-2 country code; the <c>TaxRules.Country</c> column is this wide.</summary>
    public const int CountryLength = 2;

    /// <summary>Longest region the stored column holds, as wide as a client's state.</summary>
    public const int RegionMaxLength = 100;

    /// <summary>The largest rate a rule can carry, in percent.</summary>
    public const decimal MaxRatePercent = 100m;

    /// <summary>Digits the <c>TaxRules.RatePercent</c> column holds in all, before and after the decimal point.</summary>
    public const int RatePercentPrecision = 6;

    /// <summary>Decimals the <c>TaxRules.RatePercent</c> column keeps; a finer rate would be rounded on save.</summary>
    public const int RatePercentScale = 3;

    /// <summary>Gets the display name of the rule (for example "Germany VAT").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the ISO 3166-1 alpha-2 country code the rule applies to, upper case.</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>Gets the region (state) of the country the rule is limited to, or <see langword="null"/> for a country-wide rule.</summary>
    public string? Region { get; private set; }

    /// <summary>Gets the tax rate in percent, from 0 to <see cref="MaxRatePercent"/>.</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>Gets whether new invoices use the rule.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private TaxRule() { }

    /// <summary>Creates a tax rule.</summary>
    /// <param name="name">The display name; not blank, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="country">The ISO 3166-1 alpha-2 country code; normalised to upper case.</param>
    /// <param name="region">The region the rule is limited to, or null or blank for the whole country.</param>
    /// <param name="ratePercent">The rate in percent, from 0 to <see cref="MaxRatePercent"/>.</param>
    /// <param name="isEnabled">Whether new invoices use the rule.</param>
    /// <returns>A new, unpersisted <see cref="TaxRule"/>.</returns>
    /// <exception cref="ArgumentException">The name is blank, the country is not two letters or a text is longer than its column.</exception>
    /// <exception cref="DomainRuleException">The rate is out of range.</exception>
    public static TaxRule Create(string name, string country, string? region, decimal ratePercent, bool isEnabled)
    {
        var rule = new TaxRule();
        rule.Update(name, country, region, ratePercent, isEnabled);
        return rule;
    }

    /// <summary>Replaces the rule's name, place, rate and switch.</summary>
    /// <param name="name">The display name; not blank, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="country">The ISO 3166-1 alpha-2 country code; normalised to upper case.</param>
    /// <param name="region">The region the rule is limited to, or null or blank for the whole country.</param>
    /// <param name="ratePercent">The rate in percent, from 0 to <see cref="MaxRatePercent"/>.</param>
    /// <param name="isEnabled">Whether new invoices use the rule.</param>
    /// <exception cref="ArgumentException">The name is blank, the country is not two letters or a text is longer than its column.</exception>
    /// <exception cref="DomainRuleException">The rate is out of range.</exception>
    public void Update(string name, string country, string? region, decimal ratePercent, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"A tax rule's name is at most {NameMaxLength} characters.", nameof(name));
        }

        var trimmedCountry = country.Trim();
        if (trimmedCountry.Length != CountryLength || !trimmedCountry.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("A tax rule's country is an ISO 3166-1 alpha-2 code.", nameof(country));
        }

        var trimmedRegion = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        if (trimmedRegion is { Length: > RegionMaxLength })
        {
            throw new ArgumentException($"A tax rule's region is at most {RegionMaxLength} characters.", nameof(region));
        }

        if (ratePercent is < 0m or > MaxRatePercent)
        {
            throw new DomainRuleException(BillingErrors.TaxRuleRateInvalid, "A tax rate is between 0 and 100 percent.");
        }

        Name = name.Trim();
        Country = trimmedCountry.ToUpperInvariant();
        Region = trimmedRegion;
        RatePercent = ratePercent;
        IsEnabled = isEnabled;
    }
}
