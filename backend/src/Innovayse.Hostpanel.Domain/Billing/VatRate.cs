using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The VAT rate of one EU member state from a given day on. A rate change is a new row with a later
/// <see cref="ValidFrom"/>, never an edit of the old one, so the rate in force on any day can be read back. A new invoice
/// for a consumer in another member state takes the standard row in force on the day the invoice is issued.
/// At most one row exists per country, kind and valid-from day.
/// Stored in the <c>VatRates</c> table.
/// </summary>
public sealed class VatRate : BaseEntity, IAggregateRoot
{
    /// <summary>Length of an ISO 3166-1 alpha-2 country code; the <c>VatRates.Country</c> column is this wide.</summary>
    public const int CountryLength = 2;

    /// <summary>The largest rate a row can carry, in percent; the same bound as a tax rule's.</summary>
    public const decimal MaxRatePercent = TaxRule.MaxRatePercent;

    /// <summary>Gets the ISO 3166-1 alpha-2 code of the member state, upper case, as client records store it.</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>Gets which of the member state's rates this row holds.</summary>
    public VatRateKind Kind { get; private set; }

    /// <summary>Gets the rate in percent, from 0 to <see cref="MaxRatePercent"/>.</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>Gets the first day (inclusive) the rate applies; it applies until a later row of the same country and kind starts.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private VatRate() { }

    /// <summary>Creates a VAT rate row.</summary>
    /// <param name="country">The ISO 3166-1 alpha-2 country code; normalised to upper case.</param>
    /// <param name="kind">Which of the country's rates the row holds.</param>
    /// <param name="ratePercent">The rate in percent, from 0 to <see cref="MaxRatePercent"/>.</param>
    /// <param name="validFrom">The first day the rate applies.</param>
    /// <returns>A new, unpersisted <see cref="VatRate"/>.</returns>
    /// <exception cref="ArgumentException">The country is not two letters.</exception>
    /// <exception cref="DomainRuleException">The rate is out of range.</exception>
    public static VatRate Create(string country, VatRateKind kind, decimal ratePercent, DateOnly validFrom)
    {
        var rate = new VatRate();
        rate.Update(country, kind, ratePercent, validFrom);
        return rate;
    }

    /// <summary>Replaces the row's country, kind, rate and first day.</summary>
    /// <param name="country">The ISO 3166-1 alpha-2 country code; normalised to upper case.</param>
    /// <param name="kind">Which of the country's rates the row holds.</param>
    /// <param name="ratePercent">The rate in percent, from 0 to <see cref="MaxRatePercent"/>.</param>
    /// <param name="validFrom">The first day the rate applies.</param>
    /// <exception cref="ArgumentException">The country is not two letters.</exception>
    /// <exception cref="DomainRuleException">The rate is out of range.</exception>
    public void Update(string country, VatRateKind kind, decimal ratePercent, DateOnly validFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        var trimmedCountry = country.Trim();
        if (trimmedCountry.Length != CountryLength || !trimmedCountry.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("A VAT rate's country is an ISO 3166-1 alpha-2 code.", nameof(country));
        }

        if (ratePercent is < 0m or > MaxRatePercent)
        {
            throw new DomainRuleException(BillingErrors.VatRateInvalid, "A VAT rate is between 0 and 100 percent.");
        }

        Country = trimmedCountry.ToUpperInvariant();
        Kind = kind;
        RatePercent = ratePercent;
        ValidFrom = validFrom;
    }
}
