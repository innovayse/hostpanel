using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>
/// The product bundle field rules the create and update validators share, named once. Each rule asks
/// the domain's own predicate (<see cref="ProductBundle.IsValidDomainTerm"/>,
/// <see cref="ProductBundle.IsValidDiscount"/>), so the limits live on the aggregate and a validator
/// can never accept what <see cref="ProductBundle"/> would refuse.
/// </summary>
/// <remarks>
/// Every failure carries the domain's refusal code as its FluentValidation error code, so the field
/// error and the aggregate's last-line refusal name the same rule. The response body is still the one
/// <c>VALIDATION_ERROR</c> with the field sentence.
/// </remarks>
public static class ProductBundleRuleExtensions
{
    /// <summary>Requires the registration period to agree with whether the bundle includes a domain.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the domain years property.</param>
    /// <param name="includesDomain">Reads the command's include-domain flag.</param>
    /// <param name="message">The localized sentence reported when the period does not fit.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, int?> MustMatchBundleDomainTerm<T>(
        this IRuleBuilder<T, int?> rule, Func<T, bool> includesDomain, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(includesDomain);

        return rule
            .Must((command, years) => ProductBundle.IsValidDomainTerm(includesDomain(command), years))
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_BUNDLE_DOMAIN_TERM_INVALID)
            .WithMessage(message);
    }

    /// <summary>Requires the discount amount to fit the discount type and its currency.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the discount amount property.</param>
    /// <param name="type">Reads the command's discount type.</param>
    /// <param name="currency">Reads the command's discount currency.</param>
    /// <param name="message">The localized sentence reported when the discount is incoherent.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, decimal> MustBeBundleDiscount<T>(
        this IRuleBuilder<T, decimal> rule, Func<T, BundleDiscountType> type, Func<T, string?> currency, string message)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(currency);

        return rule
            .Must((command, amount) => ProductBundle.IsValidDiscount(type(command), amount, currency(command)))
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_BUNDLE_DISCOUNT_INVALID)
            .WithMessage(message);
    }

    /// <summary>
    /// Requires at least one component, each naming a product. That the bundle then holds a primary
    /// one and no product twice is the aggregate's own refusal.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder for the items property.</param>
    /// <param name="requiredMessage">The localized sentence reported when there is no component.</param>
    /// <param name="productMessage">The localized sentence reported when a component names no product.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, IReadOnlyList<ProductBundleItemInput>> MustHaveBundleItems<T>(
        this IRuleBuilder<T, IReadOnlyList<ProductBundleItemInput>> rule, string requiredMessage, string productMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .NotEmpty()
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_BUNDLE_ITEMS_REQUIRED)
            .WithMessage(requiredMessage)
            // A missing list is already refused by the rule above; the null check only keeps this one from throwing on it.
            .Must(items => items is null || items.All(i => i.ProductId > 0))
            .WithMessage(productMessage);
    }
}
