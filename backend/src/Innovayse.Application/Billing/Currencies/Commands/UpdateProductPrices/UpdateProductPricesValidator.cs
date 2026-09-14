namespace Innovayse.Application.Billing.Currencies.Commands.UpdateProductPrices;

using FluentValidation;

/// <summary>Validates <see cref="UpdateProductPricesCommand"/> before it reaches the handler.</summary>
public sealed class UpdateProductPricesValidator : AbstractValidator<UpdateProductPricesCommand>
{
    /// <summary>Length of an ISO 4217 alpha code.</summary>
    private const int CodeLength = 3;

    /// <summary>Initialises the rules.</summary>
    public UpdateProductPricesValidator()
    {
        RuleFor(x => x.Currencies).NotEmpty();
        RuleForEach(x => x.Currencies)
            .NotEmpty()
            .Length(CodeLength)
            .Must(code => code.All(char.IsAsciiLetter))
            .WithMessage("Each currency must be a three-letter ISO 4217 code.");
    }
}
