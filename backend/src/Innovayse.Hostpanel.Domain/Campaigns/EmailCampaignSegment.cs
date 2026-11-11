using System.Globalization;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// One rule that picks recipients for an <see cref="EmailCampaign"/>; the recipients of a campaign are the
/// clients matched by any of its segments. Stored in the <c>EmailCampaignSegments</c> table.
/// </summary>
public sealed class EmailCampaignSegment : BaseEntity
{
    /// <summary>Longest filter value the stored column holds.</summary>
    public const int FilterValueMaxLength = 100;

    /// <summary>Most days ahead a service expiry filter looks.</summary>
    public const int MaxExpiringDays = 365;

    /// <summary>Length of a country code.</summary>
    private const int CountryCodeLength = 2;

    /// <summary>Gets the FK to the parent <see cref="EmailCampaign"/>.</summary>
    public int CampaignId { get; private set; }

    /// <summary>Gets the kind of rule the segment uses.</summary>
    public CampaignFilterType FilterType { get; private set; }

    /// <summary>
    /// Gets the value the rule needs: a product id for <see cref="CampaignFilterType.Product"/>, a two-letter upper-case
    /// country code for <see cref="CampaignFilterType.Country"/>, a number of days for
    /// <see cref="CampaignFilterType.ServiceExpiringInDays"/>; <see langword="null"/> for <see cref="CampaignFilterType.AllActive"/>.
    /// </summary>
    public string? FilterValue { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private EmailCampaignSegment() { }

    /// <summary>Builds a segment once its value is known to fit its filter.</summary>
    /// <param name="definition">What the caller wants the segment to be.</param>
    /// <returns>A new <see cref="EmailCampaignSegment"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="DomainRuleException">The value does not fit the filter.</exception>
    internal static EmailCampaignSegment Create(CampaignSegmentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new EmailCampaignSegment
        {
            FilterType = definition.FilterType,
            FilterValue = Normalize(definition.FilterType, definition.FilterValue),
        };
    }

    /// <summary>Describes the segment the way a caller writes one, so it can be read out or matched against clients.</summary>
    /// <returns>The segment's filter and value.</returns>
    public CampaignSegmentDefinition ToDefinition()
    {
        return new CampaignSegmentDefinition(FilterType, FilterValue);
    }

    /// <summary>Tells whether a value fits a filter, so a caller can refuse it before it builds a campaign.</summary>
    /// <param name="filterType">The filter the value belongs to.</param>
    /// <param name="value">The value as given.</param>
    /// <returns><see langword="true"/> when <see cref="EmailCampaign"/> would accept the pair.</returns>
    public static bool Accepts(CampaignFilterType filterType, string? value)
    {
        return Check(filterType, value, out _) is null;
    }

    /// <summary>Checks a value against its filter and puts it in the shape it is stored in.</summary>
    /// <param name="filterType">The filter the value belongs to.</param>
    /// <param name="value">The value as given.</param>
    /// <returns>The value to store, or <see langword="null"/> for a filter that takes none.</returns>
    /// <exception cref="DomainRuleException">The value does not fit the filter.</exception>
    public static string? Normalize(CampaignFilterType filterType, string? value)
    {
        var problem = Check(filterType, value, out var normalized);
        if (problem is not null)
        {
            throw Invalid(problem);
        }

        return normalized;
    }

    /// <summary>The one place that decides whether a value fits a filter and what shape it is stored in.</summary>
    /// <param name="filterType">The filter the value belongs to.</param>
    /// <param name="value">The value as given.</param>
    /// <param name="normalized">The value to store; <see langword="null"/> for a filter that takes none, and when there is a problem.</param>
    /// <returns>What was violated, or <see langword="null"/> when the value fits.</returns>
    private static string? Check(CampaignFilterType filterType, string? value, out string? normalized)
    {
        var trimmed = value?.Trim();
        normalized = null;

        switch (filterType)
        {
            case CampaignFilterType.AllActive:
                return string.IsNullOrEmpty(trimmed) ? null : "Filtering on all active clients takes no value.";

            case CampaignFilterType.Product:
                return CheckPositiveNumber(trimmed, int.MaxValue, "A product filter needs a positive product id.", out normalized);

            case CampaignFilterType.Country:
                var country = CountryCode.Normalize(trimmed);
                if (country is null || country.Length != CountryCodeLength || !country.All(char.IsAsciiLetterUpper))
                {
                    return "A country filter needs a two-letter country code.";
                }

                normalized = country;
                return null;

            case CampaignFilterType.ServiceExpiringInDays:
                return CheckPositiveNumber(trimmed, MaxExpiringDays, $"An expiry filter needs a number of days from 1 to {MaxExpiringDays}.", out normalized);

            default:
                return "Unknown filter type.";
        }
    }

    /// <summary>Checks a value is a whole number from 1 up to a limit and writes it in invariant form.</summary>
    /// <param name="value">The trimmed value.</param>
    /// <param name="max">The largest accepted number.</param>
    /// <param name="message">What to say when it is not.</param>
    /// <param name="normalized">The number as stored; <see langword="null"/> when there is a problem.</param>
    /// <returns><paramref name="message"/> when the value is not a whole number in range, otherwise <see langword="null"/>.</returns>
    private static string? CheckPositiveNumber(string? value, int max, string message, out string? normalized)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > max)
        {
            normalized = null;
            return message;
        }

        normalized = number.ToString(CultureInfo.InvariantCulture);
        return null;
    }

    /// <summary>Builds the refusal for a value that does not fit its filter.</summary>
    /// <param name="message">What was violated, for the log.</param>
    /// <returns>The exception to throw.</returns>
    private static DomainRuleException Invalid(string message)
    {
        return new DomainRuleException(CampaignErrors.SegmentValueInvalid, message);
    }
}
