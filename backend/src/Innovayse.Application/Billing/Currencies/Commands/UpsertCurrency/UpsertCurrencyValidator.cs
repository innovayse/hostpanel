namespace Innovayse.Application.Billing.Currencies.Commands.UpsertCurrency;

using FluentValidation;

/// <summary>Validates <see cref="UpsertCurrencyCommand"/> before it reaches the handler.</summary>
public sealed class UpsertCurrencyValidator : AbstractValidator<UpsertCurrencyCommand>
{
    /// <summary>Length of both ISO 4217 codes: three letters, three digits.</summary>
    private const int CodeLength = 3;

    /// <summary>
    /// The most decimal places any ISO 4217 currency carries (three, for the dinars); one more
    /// is allowed so an operator can price a low-value unit finely without inventing a currency.
    /// </summary>
    private const int MaxDecimals = 4;

    /// <summary>Longest prefix or suffix the display will fit.</summary>
    private const int MaxAffixLength = 8;

    /// <summary>Initialises the rules.</summary>
    public UpsertCurrencyValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(CodeLength)
            .Must(code => code.All(char.IsAsciiLetter))
            .WithMessage("'Code' must be a three-letter ISO 4217 code.");

        RuleFor(x => x.Numeric)
            .NotEmpty()
            .Length(CodeLength)
            .Must(numeric => numeric.All(char.IsAsciiDigit))
            .WithMessage("'Numeric' must be a three-digit ISO 4217 code.");

        RuleFor(x => x.Prefix).NotNull().MaximumLength(MaxAffixLength);
        RuleFor(x => x.Suffix).NotNull().MaximumLength(MaxAffixLength);
        RuleFor(x => x.Decimals).InclusiveBetween(0, MaxDecimals);
        RuleFor(x => x.RateToBase).GreaterThan(0m);
    }
}
