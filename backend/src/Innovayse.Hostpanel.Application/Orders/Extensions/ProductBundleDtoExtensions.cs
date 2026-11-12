using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Orders.Extensions;

/// <summary>Extension methods the order flow uses to place an order from a <see cref="ProductBundleDto"/>.</summary>
public static class ProductBundleDtoExtensions
{
    /// <summary>
    /// Checks the lines of a checkout against the bundle it names and returns them ready to be priced.
    /// </summary>
    /// <remarks>
    /// The product lines (those with no domain action) must be exactly the bundle's products, each once. A domain line
    /// must be present exactly when the bundle includes a domain, and it may register or transfer but not renew. The
    /// registration period of the domain line is the bundle's <see cref="ProductBundleDto.DomainYears"/>: whatever
    /// period the line sent is replaced, so a client can neither shorten the period it pays the bundle price for nor
    /// buy a longer one at that price.
    /// </remarks>
    /// <param name="bundle">The bundle the order is placed from.</param>
    /// <param name="items">The checkout's lines.</param>
    /// <returns>
    /// The lines with the domain line's period set to the bundle's, or <c>PRODUCT_BUNDLE_NOT_AVAILABLE</c> when the
    /// bundle is not active, <c>ORDER_BUNDLE_ITEMS_MISMATCH</c> when the product lines are not the bundle's products,
    /// <c>ORDER_BUNDLE_DOMAIN_MISMATCH</c> when the domain lines do not agree with the bundle's included domain, or
    /// <c>ORDER_BUNDLE_DOMAIN_ACTION_NOT_ALLOWED</c> when the domain line renews.
    /// </returns>
    public static Result<IReadOnlyList<PlaceOrderItemDto>> ResolveOrderItems(
        this ProductBundleDto bundle, IReadOnlyList<PlaceOrderItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(items);

        if (!bundle.IsActive)
        {
            return Result<IReadOnlyList<PlaceOrderItemDto>>.FailureCode(HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_AVAILABLE);
        }

        var productIds = items.Where(i => i.DomainAction is null).Select(i => i.ProductId).Order();
        if (!productIds.SequenceEqual(bundle.Components.Select(c => c.ProductId).Order()))
        {
            return Result<IReadOnlyList<PlaceOrderItemDto>>.FailureCode(HostpanelErrorCodes.ORDER_BUNDLE_ITEMS_MISMATCH);
        }

        var domainLines = items.Count(i => i.DomainAction is not null);
        if (domainLines != (bundle.IncludesDomain ? 1 : 0))
        {
            return Result<IReadOnlyList<PlaceOrderItemDto>>.FailureCode(HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_MISMATCH);
        }

        if (items.Any(i => i.DomainAction == DomainAction.Renew))
        {
            return Result<IReadOnlyList<PlaceOrderItemDto>>.FailureCode(HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_ACTION_NOT_ALLOWED);
        }

        IReadOnlyList<PlaceOrderItemDto> resolved = items
            .Select(i => i.DomainAction is null ? i : i with { Years = bundle.DomainYears })
            .ToList();
        return Result<IReadOnlyList<PlaceOrderItemDto>>.Success(resolved);
    }
}
