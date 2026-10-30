using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The product field rules the create and update validators share, named once. Each rule asks the
/// domain's own predicate (<see cref="ProductSku.IsValid"/>, <see cref="Product.IsWithinMetadataSize"/>,
/// <see cref="Product.IsValidMetadata"/>, <see cref="Product.IsValidGracePeriod"/>), so the limits live
/// on the aggregate and a validator can never accept what <see cref="Product"/> would refuse.
/// </summary>
/// <remarks>
/// Every failure carries the domain's refusal code as its FluentValidation error code, so the field
/// error and the aggregate's last-line refusal name the same rule. The response body is still the one
/// <c>VALIDATION_ERROR</c> with the field sentence; the code is for whoever reads the failure in code.
/// </remarks>
public static class ProductRuleExtensions
{
    /// <summary>
    /// Requires a SKU that passes <see cref="ProductSku.IsValid"/> once normalised. A blank value passes:
    /// on create it means "no SKU", on update it clears the stored one.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the SKU property.</param>
    /// <param name="message">The localized sentence reported when the SKU has another shape.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeProductSku<T>(this IRuleBuilder<T, string?> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(sku => string.IsNullOrWhiteSpace(sku) || ProductSku.IsValid(sku))
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_SKU_INVALID)
            .WithMessage(message);
    }

    /// <summary>
    /// Requires metadata that fits <see cref="Product.MaxMetadataBytes"/> and is a JSON object. A blank
    /// value passes: on create it means "no metadata", on update it clears the stored bag.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the metadata property.</param>
    /// <param name="tooLargeMessage">The localized sentence reported when the text is over the size cap.</param>
    /// <param name="invalidMessage">The localized sentence reported when the text is not a JSON object.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeProductMetadata<T>(
        this IRuleBuilderInitial<T, string?> rule, string tooLargeMessage, string invalidMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        // The size is checked first and stops the chain, so an oversized body is never parsed.
        return rule
            .Cascade(CascadeMode.Stop)
            .Must(Product.IsWithinMetadataSize)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithMessage(tooLargeMessage)
            .Must(metadata => string.IsNullOrWhiteSpace(metadata) || Product.IsValidMetadata(metadata))
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithMessage(invalidMessage);
    }

    /// <summary>
    /// Requires a grace period that passes <see cref="Product.IsValidGracePeriod"/>. An absent value
    /// passes: on create the product keeps its default, on update the stored value stays.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the grace period property.</param>
    /// <param name="message">The localized sentence reported when the period is out of range.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int?> MustBeGracePeriod<T>(this IRuleBuilder<T, int?> rule, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(days => days is null || Product.IsValidGracePeriod(days.Value))
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_GRACE_PERIOD_OUT_OF_RANGE)
            .WithMessage(message);
    }
}
