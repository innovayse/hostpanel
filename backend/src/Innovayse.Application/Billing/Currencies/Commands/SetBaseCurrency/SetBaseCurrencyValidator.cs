namespace Innovayse.Application.Billing.Currencies.Commands.SetBaseCurrency;

using FluentValidation;

/// <summary>Validates <see cref="SetBaseCurrencyCommand"/> before it reaches the handler.</summary>
public sealed class SetBaseCurrencyValidator : AbstractValidator<SetBaseCurrencyCommand>
{
    /// <summary>Length of an ISO 4217 alpha code.</summary>
    private const int CodeLength = 3;

    /// <summary>Initialises the rules.</summary>
    public SetBaseCurrencyValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(CodeLength)
            .Must(code => code.All(char.IsAsciiLetter))
            .WithMessage("'Code' must be a three-letter ISO 4217 code.");
    }
}
