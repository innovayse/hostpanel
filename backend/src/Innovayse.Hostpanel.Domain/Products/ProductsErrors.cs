namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// Refusal codes the <see cref="Product"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for each one in every language.
/// SKU uniqueness is not here: it spans products, so the handler checks it and answers with
/// <c>PRODUCT_SKU_ALREADY_IN_USE</c>.
/// </remarks>
public static class ProductsErrors
{
    /// <summary>
    /// The SKU does not match <see cref="ProductSku.Pattern"/> or is longer than
    /// <see cref="ProductSku.MaxLength"/> after normalisation (400): <see cref="Product.SetSku"/>.
    /// </summary>
    public const string ProductSkuInvalid = "PRODUCT_SKU_INVALID";

    /// <summary>
    /// The metadata is not a JSON object or is larger than <see cref="Product.MaxMetadataBytes"/> (400):
    /// <see cref="Product.SetMetadata"/>.
    /// </summary>
    public const string ProductMetadataInvalid = "PRODUCT_METADATA_INVALID";

    /// <summary>
    /// The grace period is outside <see cref="Product.MinGracePeriodDays"/> to
    /// <see cref="Product.MaxGracePeriodDays"/> days (400): <see cref="Product.SetGracePeriodDays"/>.
    /// </summary>
    public const string ProductGracePeriodOutOfRange = "PRODUCT_GRACE_PERIOD_OUT_OF_RANGE";

    /// <summary>
    /// The free-trial settings are outside what is accepted (400): a length outside
    /// <see cref="ProductTrial.MinDays"/> to <see cref="ProductTrial.MaxDays"/> days, a grace period outside
    /// <see cref="ProductTrial.MinGracePeriodDays"/> to <see cref="ProductTrial.MaxGracePeriodDays"/> days, or
    /// reminder days that repeat, number more than <see cref="ProductTrial.MaxReminders"/> or do not fall
    /// before the end of the trial: <see cref="Product.ConfigureTrial"/>.
    /// </summary>
    public const string ProductTrialInvalid = "PRODUCT_TRIAL_INVALID";

    /// <summary>
    /// A bundle with a domain names no 1 to 10 year period, or one without a domain names a period (400):
    /// <see cref="ProductBundle.Update"/>.
    /// </summary>
    public const string ProductBundleDomainTermInvalid = "PRODUCT_BUNDLE_DOMAIN_TERM_INVALID";

    /// <summary>
    /// The discount type, amount and currency do not agree: a percentage is from 0 to 100, a fixed amount
    /// is not negative and carries a currency, no discount carries neither (400): <see cref="ProductBundle.Update"/>.
    /// </summary>
    public const string ProductBundleDiscountInvalid = "PRODUCT_BUNDLE_DISCOUNT_INVALID";

    /// <summary>The bundle has no product in it (400): <see cref="ProductBundle.Update"/>.</summary>
    public const string ProductBundleItemsRequired = "PRODUCT_BUNDLE_ITEMS_REQUIRED";

    /// <summary>The same product is listed in the bundle more than once (400): <see cref="ProductBundle.Update"/>.</summary>
    public const string ProductBundleItemDuplicate = "PRODUCT_BUNDLE_ITEM_DUPLICATE";

    /// <summary>The bundle does not have exactly one primary product (400): <see cref="ProductBundle.Update"/>.</summary>
    public const string ProductBundlePrimaryInvalid = "PRODUCT_BUNDLE_PRIMARY_INVALID";

    /// <summary>
    /// The quantity range does not fit the group: a quantity group needs a minimum from zero and a maximum
    /// of at least one and not below it, and every other type must carry none (400):
    /// <see cref="ProductOptionGroup.Create"/> and <see cref="ProductOptionGroup.Update"/>.
    /// </summary>
    public const string ProductOptionQuantityRangeInvalid = "PRODUCT_OPTION_QUANTITY_RANGE_INVALID";

    /// <summary>
    /// The group's type holds one option and already has it (400): <see cref="ProductOptionGroup.AddOption"/>.
    /// </summary>
    public const string ProductOptionCountNotAllowed = "PRODUCT_OPTION_COUNT_NOT_ALLOWED";

    /// <summary>
    /// A text option was given a price, which a free text value cannot carry (400):
    /// <see cref="ProductOptionGroup.AddOption"/> and <see cref="ProductOptionGroup.UpdateOption"/>.
    /// </summary>
    public const string ProductOptionPriceNotAllowed = "PRODUCT_OPTION_PRICE_NOT_ALLOWED";

    /// <summary>
    /// An option modifier is negative, or a percentage above 100 (400):
    /// <see cref="ProductOptionGroup.AddOption"/> and <see cref="ProductOptionGroup.UpdateOption"/>.
    /// </summary>
    public const string ProductOptionPriceOutOfRange = "PRODUCT_OPTION_PRICE_OUT_OF_RANGE";

    /// <summary>
    /// The id names no option of the group (404): <see cref="ProductOptionGroup.UpdateOption"/> and
    /// <see cref="ProductOptionGroup.RemoveOption"/>.
    /// </summary>
    public const string ProductOptionNotFound = "PRODUCT_OPTION_NOT_FOUND";
}
