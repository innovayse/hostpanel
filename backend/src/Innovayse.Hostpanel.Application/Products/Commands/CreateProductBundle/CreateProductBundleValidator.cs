using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductBundle;

/// <summary>Validates <see cref="CreateProductBundleCommand"/> before it reaches the handler.</summary>
public sealed class CreateProductBundleValidator : LocalizedValidator<CreateProductBundleCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateProductBundleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductBundleNameRequired"])
            .MaximumLength(ProductBundle.NameMaxLength).WithMessage(L["ProductBundleNameTooLong", ProductBundle.NameMaxLength]);
        RuleFor(x => x.Description).MaximumLength(ProductBundle.DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage(L["ProductBundleDescriptionTooLong", ProductBundle.DescriptionMaxLength]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage(L["ProductBundleSortOrderInvalid"]);
        RuleFor(x => x.DomainYears).MustMatchBundleDomainTerm(
            x => x.IncludesDomain, L["ProductBundleDomainTermInvalid", ProductBundle.MaxDomainYears]);
        RuleFor(x => x.DiscountAmount).MustBeBundleDiscount(
            x => x.DiscountType, x => x.DiscountCurrency, L["ProductBundleDiscountInvalid", ProductBundle.MaxDiscountPercentage]);
        RuleFor(x => x.Items).MustHaveBundleItems(L["ProductBundleItemsRequired"], L["ProductBundleItemProductInvalid"]);
    }
}
