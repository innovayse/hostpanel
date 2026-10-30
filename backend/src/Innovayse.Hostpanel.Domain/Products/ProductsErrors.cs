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
}
