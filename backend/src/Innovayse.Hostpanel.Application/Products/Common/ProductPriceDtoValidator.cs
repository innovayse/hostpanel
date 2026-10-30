using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>Validates one <see cref="ProductPriceDto"/>; shared by the create and update validators.</summary>
public sealed class ProductPriceDtoValidator : LocalizedValidator<ProductPriceDto>
{
    /// <summary>Length of an ISO 4217 alpha code.</summary>
    private const int CurrencyCodeLength = 3;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ProductPriceDtoValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CurrencyCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(CurrencyCodeLength)
            .Must(code => code.All(char.IsAsciiLetter))
            .WithMessage(L["ProductPriceCurrencyInvalid"]);
        foreach (var cycle in Enum.GetValues<BillingCycle>())
        {
            RuleFor(x => x.For(cycle)).GreaterThanOrEqualTo(0).When(x => x.For(cycle) is not null)
                .WithMessage(L["ProductPriceAmountInvalid"]);
        }
    }
}
