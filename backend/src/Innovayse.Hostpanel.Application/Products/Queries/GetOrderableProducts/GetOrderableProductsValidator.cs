using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProducts;

/// <summary>Validates <see cref="GetOrderableProductsQuery"/>.</summary>
public sealed class GetOrderableProductsValidator : LocalizedValidator<GetOrderableProductsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetOrderableProductsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleForEach(x => x.ProductIds).GreaterThan(0).WithMessage(L["ProductIdInvalid"]);
        RuleFor(x => x.Currency).MustBeCurrencyCode(L["CurrencyCodeInvalid"]);
    }
}
