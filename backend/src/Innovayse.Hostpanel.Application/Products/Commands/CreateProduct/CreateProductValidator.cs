using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProduct;

/// <summary>Validates <see cref="CreateProductCommand"/> before it reaches the handler.</summary>
public sealed class CreateProductValidator : LocalizedValidator<CreateProductCommand>
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
    public CreateProductValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GroupId).MustBePositiveId(L["ProductGroupIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductNameRequired"])
            .MaximumLength(NameMaxLength).WithMessage(L["ProductNameTooLong", NameMaxLength]);
        RuleFor(x => x.Description).MaximumLength(DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage(L["ProductDescriptionTooLong", DescriptionMaxLength]);
        RuleFor(x => x.Website).MaximumLength(Product.WebsiteMaxLength).When(x => x.Website is not null)
            .WithMessage(L["ContentUrlTooLong", Product.WebsiteMaxLength]);
        RuleFor(x => x.PackageName).MaximumLength(PackageNameMaxLength).When(x => x.PackageName is not null)
            .WithMessage(L["ProductPackageNameTooLong", PackageNameMaxLength]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0)
            .WithMessage(L["ProductSortOrderInvalid"]);

        // A new product must be sellable somewhere; an update may clear every price (see
        // UpdateProductValidator). Without a per-currency list the legacy pair must carry a figure.
        RuleFor(x => x.Prices).NotEmpty().When(x => x.Prices is not null)
            .WithMessage(L["ProductPriceRequired"]);
        RuleFor(x => x.MonthlyPrice).NotNull().When(x => x.Prices is null && x.AnnualPrice is null)
            .WithMessage(L["ProductPriceRequired"]);
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
