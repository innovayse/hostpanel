using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpsertCurrency;

/// <summary>
///     Validator for UpsertCurrencyCommand.
///     Ensures currency data integrity before the row is written.
/// </summary>
public sealed class UpsertCurrencyValidator : LocalizedValidator<UpsertCurrencyCommand>
{
    /// <summary>Length of an ISO 4217 alpha or numeric code.</summary>
    private const int CodeLength = 3;

    /// <summary>Most decimal places an amount may be shown to.</summary>
    private const int MaxDecimals = 4;

    /// <summary>Longest prefix or suffix printed around an amount.</summary>
    private const int MaxAffixLength = 8;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpsertCurrencyValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Code).MustBeCurrencyCode(L["CurrencyCodeInvalid"]);

        RuleFor(x => x.Numeric)
            .NotEmpty()
            .Length(CodeLength)
            .Must(numeric => numeric.All(char.IsAsciiDigit))
            .WithMessage(L["CurrencyNumericInvalid"]);

        RuleFor(x => x.Prefix)
            .NotNull()
            .MaximumLength(MaxAffixLength)
            .WithMessage(L["CurrencyAffixInvalid", MaxAffixLength]);

        RuleFor(x => x.Suffix)
            .NotNull()
            .MaximumLength(MaxAffixLength)
            .WithMessage(L["CurrencyAffixInvalid", MaxAffixLength]);

        RuleFor(x => x.Decimals)
            .InclusiveBetween(0, MaxDecimals)
            .WithMessage(L["CurrencyDecimalsInvalid", MaxDecimals]);

        RuleFor(x => x.RateToBase)
            .GreaterThan(0m)
            .WithMessage(L["CurrencyRateInvalid"]);
    }
}
