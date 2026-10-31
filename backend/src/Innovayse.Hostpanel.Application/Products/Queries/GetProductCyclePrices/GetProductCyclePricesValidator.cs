using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;

/// <summary>Validates <see cref="GetProductCyclePricesQuery"/> before it reaches the handler.</summary>
public sealed class GetProductCyclePricesValidator : LocalizedValidator<GetProductCyclePricesQuery>
{
    /// <summary>Initializes the validator with the localized messages.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetProductCyclePricesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleForEach(x => x.Lookups).ChildRules(lookup =>
        {
            lookup.RuleFor(l => l.ProductId).GreaterThan(0).WithMessage(L["ProductIdInvalid"]);
            lookup.RuleFor(l => l.CurrencyCode).MustBeCurrencyCode(L["ProductPriceCurrencyInvalid"]);
            lookup.RuleFor(l => l.Cycle).IsInEnum().WithMessage(L["ServiceBillingCycleInvalid"]);
        });
    }
}
