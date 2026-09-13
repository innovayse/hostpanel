namespace Innovayse.Application.Products.Common;

using FluentValidation;

/// <summary>Validates one <see cref="ProductPriceInput"/>; shared by the create and update validators.</summary>
public sealed class ProductPriceInputValidator : AbstractValidator<ProductPriceInput>
{
    /// <summary>Length of an ISO 4217 alpha code.</summary>
    private const int CurrencyCodeLength = 3;

    /// <summary>Initializes validation rules for a per-currency price entry.</summary>
    public ProductPriceInputValidator()
    {
        RuleFor(x => x.CurrencyCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(CurrencyCodeLength)
            .Must(code => code.All(char.IsAsciiLetter))
            .WithMessage("Currency code must be three letters.");
        RuleFor(x => x.Monthly).GreaterThanOrEqualTo(0).When(x => x.Monthly is not null);
        RuleFor(x => x.Annual).GreaterThanOrEqualTo(0).When(x => x.Annual is not null);
    }
}
