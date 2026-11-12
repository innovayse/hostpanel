using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.AddProductOption;

/// <summary>
/// Validates <see cref="AddProductOptionCommand"/> before it reaches the handler. The rules that depend
/// on the group's type, or on the price type, stay in the aggregate.
/// </summary>
public sealed class AddProductOptionValidator : LocalizedValidator<AddProductOptionCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AddProductOptionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GroupId).MustBePositiveId(L["ProductOptionGroupIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductOptionNameRequired"])
            .MaximumLength(ProductOption.NameMaxLength)
            .WithMessage(L["ProductOptionNameTooLong", ProductOption.NameMaxLength]);
        RuleFor(x => x.PriceModifier).GreaterThanOrEqualTo(0m).WithMessage(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.PriceType).IsInEnum().WithMessage(L["ProductOptionPriceTypeInvalid"]);
        RuleFor(x => x.SetupFeeModifier).GreaterThanOrEqualTo(0m).WithMessage(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage(L["ProductOptionDisplayOrderInvalid"]);
    }
}
