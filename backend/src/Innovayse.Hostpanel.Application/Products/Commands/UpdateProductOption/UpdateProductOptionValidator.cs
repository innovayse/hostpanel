using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOption;

/// <summary>
/// Validates <see cref="UpdateProductOptionCommand"/> before it reaches the handler. The rules that depend
/// on the group's type, or on the price type, stay in the aggregate.
/// </summary>
public sealed class UpdateProductOptionValidator : LocalizedValidator<UpdateProductOptionCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateProductOptionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductOptionIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductOptionNameRequired"])
            .MaximumLength(ProductOption.NameMaxLength)
            .WithMessage(L["ProductOptionNameTooLong", ProductOption.NameMaxLength]);
        RuleFor(x => x.PriceModifier).GreaterThanOrEqualTo(0m).WithMessage(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.PriceType).IsInEnum().WithMessage(L["ProductOptionPriceTypeInvalid"]);
        RuleFor(x => x.SetupFeeModifier).GreaterThanOrEqualTo(0m).WithMessage(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage(L["ProductOptionDisplayOrderInvalid"]);
    }
}
