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
        RuleFor(x => x.Name).MustBeName(
            ProductOption.NameMaxLength,
            L["ProductOptionNameRequired"],
            L["ProductOptionNameTooLong", ProductOption.NameMaxLength]);
        RuleFor(x => x.PriceModifier).MustBeNonNegative(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.PriceType).MustBeDefinedEnum(L["ProductOptionPriceTypeInvalid"]);
        RuleFor(x => x.SetupFeeModifier).MustBeNonNegative(L["ProductOptionPriceInvalid"]);
        RuleFor(x => x.DisplayOrder).MustBeNonNegative(L["ProductOptionDisplayOrderInvalid"]);
    }
}
