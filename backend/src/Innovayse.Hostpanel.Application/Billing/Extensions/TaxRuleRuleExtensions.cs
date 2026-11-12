using FluentValidation;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Rule chains for the fields a tax rule is created and updated with, named once so the two validators cannot
/// drift apart. The limits come from <see cref="TaxRule"/>.
/// </summary>
public static class TaxRuleRuleExtensions
{
    /// <summary>Requires a rule name: not blank, within the stored column.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the name property.</param>
    /// <param name="message">The localized sentence reported when the name is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeTaxRuleName<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.NotEmpty().MaximumLength(TaxRule.NameMaxLength).WithMessage(message);
    }

    /// <summary>Requires a country written as two letters.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the country property.</param>
    /// <param name="message">The localized sentence reported when the country is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeTaxRuleCountry<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.NotEmpty().Length(TaxRule.CountryLength).Must(c => c.All(char.IsAsciiLetter)).WithMessage(message);
    }

    /// <summary>Requires a region, when one is given, to fit the stored column.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the region property.</param>
    /// <param name="message">The localized sentence reported when the region is too long.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeTaxRuleRegion<T>(this IRuleBuilder<T, string?> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.MaximumLength(TaxRule.RegionMaxLength).WithMessage(message);
    }

    /// <summary>Requires a rate between 0 and 100 percent, with at most the decimals the stored column keeps.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the rate property.</param>
    /// <param name="rangeMessage">The localized sentence reported when the rate is out of range.</param>
    /// <param name="decimalsMessage">The localized sentence reported when the rate has more decimals than the column keeps.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> MustBeTaxRate<T>(
        this IRuleBuilder<T, decimal> rule, string rangeMessage, string decimalsMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.InclusiveBetween(0m, TaxRule.MaxRatePercent).WithMessage(rangeMessage)
            .PrecisionScale(TaxRule.RatePercentPrecision, TaxRule.RatePercentScale, ignoreTrailingZeros: true)
            .WithMessage(decimalsMessage);
    }
}
