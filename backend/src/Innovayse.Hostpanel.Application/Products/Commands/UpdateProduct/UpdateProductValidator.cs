using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProduct;

/// <summary>Validates <see cref="UpdateProductCommand"/> before it reaches the handler.</summary>
public sealed class UpdateProductValidator : LocalizedValidator<UpdateProductCommand>
{
    /// <summary>Longest a product name may be.</summary>
    private const int NameMaxLength = 200;

    /// <summary>Longest a description may be.</summary>
    private const int DescriptionMaxLength = 1000;

    /// <summary>Longest a hosting package name may be.</summary>
    private const int PackageNameMaxLength = 100;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateProductValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductNameRequired"])
            .MaximumLength(NameMaxLength).WithMessage(L["ProductNameTooLong", NameMaxLength]);
        RuleFor(x => x.Description).MaximumLength(DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage(L["ProductDescriptionTooLong", DescriptionMaxLength]);
        RuleFor(x => x.Website).MaximumLength(Product.WebsiteMaxLength).When(x => x.Website is not null)
            .WithMessage(L["ContentUrlTooLong", Product.WebsiteMaxLength]);
        RuleFor(x => x.PackageName).MaximumLength(PackageNameMaxLength).When(x => x.PackageName is not null)
            .WithMessage(L["ProductPackageNameTooLong", PackageNameMaxLength]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder is not null)
            .WithMessage(L["ProductSortOrderInvalid"]);

        // An update may legitimately clear every price: the product becomes unsellable and the
        // admin grid shows it as such. Only the entries that are present are checked; the legacy
        // single-currency pair stands in for the list when a caller sends that instead.
        RuleForEach(x => x.Prices).SetValidator(new ProductPriceDtoValidator(localizer)).When(x => x.Prices is not null);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0).When(x => x.MonthlyPrice is not null)
            .WithMessage(L["ProductPriceAmountInvalid"]);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0).When(x => x.AnnualPrice is not null)
            .WithMessage(L["ProductPriceAmountInvalid"]);

        // The fulfilment fields ask the aggregate's own predicates, so the limits are written once.
        RuleFor(x => x.Sku).MustBeProductSku(L["ProductSkuInvalid", ProductSku.MaxLength]);
        RuleFor(x => x.Metadata).MustBeProductMetadata(
            L["ProductMetadataTooLarge", Product.MaxMetadataBytes], L["ProductMetadataInvalid"]);
        RuleFor(x => x.GracePeriodDays).MustBeGracePeriod(
            L["ProductGracePeriodOutOfRange", Product.MinGracePeriodDays, Product.MaxGracePeriodDays]);
    }
}
