using FluentValidation;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Rule chains for the fields a VAT rate is created and updated with, named once so the two validators cannot drift
/// apart. The rate itself is checked by <see cref="TaxRuleRuleExtensions.MustBeTaxRate{T}"/>, which holds the same bounds.
/// </summary>
public static class VatRateRuleExtensions
{
    /// <summary>The earliest first day a rate may carry: the year the single market's VAT rules took their present shape.</summary>
    private const int MinValidFromYear = 1993;

    /// <summary>The latest year a rate may start in.</summary>
    private const int MaxValidFromYear = 2100;

    /// <summary>Requires a country that is an EU member state, written as its two-letter code.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the country property.</param>
    /// <param name="message">The localized sentence reported when the country is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeMemberState<T>(this IRuleBuilder<T, string> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .NotEmpty()
            .Length(VatRate.CountryLength)
            .Must(c => EuMemberStates.Codes.Contains(c.ToUpperInvariant()))
            .WithMessage(message);
    }

    /// <summary>Requires a kind that names a <see cref="VatRateKind"/>.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the kind property.</param>
    /// <param name="message">The localized sentence reported when the kind is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, VatRateKind> MustBeVatRateKind<T>(this IRuleBuilder<T, VatRateKind> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.IsInEnum().WithMessage(message);
    }

    /// <summary>Requires a first day between the start of <see cref="MinValidFromYear"/> and the end of <see cref="MaxValidFromYear"/>.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the first-day property.</param>
    /// <param name="message">The localized sentence reported when the day is refused.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, DateOnly> MustBeVatRateStart<T>(this IRuleBuilder<T, DateOnly> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Must(d => d.Year is >= MinValidFromYear and <= MaxValidFromYear).WithMessage(message);
    }
}
