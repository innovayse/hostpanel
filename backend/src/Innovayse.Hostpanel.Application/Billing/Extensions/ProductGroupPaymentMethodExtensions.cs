using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one rule for which payment methods the product groups of a cart or an invoice allow. The list a checkout
/// shows, the order it places and the payment started on an invoice all ask it, so what is offered and what is
/// accepted cannot drift apart.
/// </summary>
public static class ProductGroupPaymentMethodExtensions
{
    /// <summary>
    /// Works out the payment methods allowed by every product group the given products and invoice belong to.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="productIds">The products of a cart; an id that names no product is ignored.</param>
    /// <param name="invoiceId">
    /// An invoice whose lines charge services of products, or <see langword="null"/>; a line that charges no service has no group.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with <see langword="null"/> when no group is restricted (every method on offer may be used), or with the
    /// methods all restricted groups share; <c>PAYMENT_NO_COMMON_METHOD</c> when they share none.
    /// </returns>
    public static async Task<Result<IReadOnlyList<string>?>> AllowedPaymentMethodsAsync(
        this IHostpanelDbContext context, IReadOnlyCollection<int> productIds, int? invoiceId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(productIds);

        var groupIds = context.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.GroupId);

        if (invoiceId is { } id)
        {
            var chargedProductIds = context.ClientServices
                .Where(s => context.InvoiceItems.Any(i => i.InvoiceId == id && i.ClientServiceId == s.Id))
                .Select(s => s.ProductId);
            groupIds = groupIds.Concat(context.Products.Where(p => chargedProductIds.Contains(p.Id)).Select(p => p.GroupId));
        }

        var groups = await context.ProductGroups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(ct);

        var common = ProductGroup.CommonAllowedPaymentMethods(groups);
        return common is { Count: 0 }
            ? Result<IReadOnlyList<string>?>.FailureCode(HostpanelErrorCodes.PAYMENT_NO_COMMON_METHOD)
            : Result<IReadOnlyList<string>?>.Success(common);
    }

    /// <summary>Whether a payment method may be used under the restriction the groups impose.</summary>
    /// <param name="allowed">What <see cref="AllowedPaymentMethodsAsync"/> answered; <see langword="null"/> for no restriction.</param>
    /// <param name="module">The payment method module id.</param>
    /// <returns><see langword="true"/> when nothing is restricted or the module is among the allowed ones (ignoring case).</returns>
    public static bool Permits(this IReadOnlyList<string>? allowed, string module)
    {
        return allowed is null || allowed.Contains(module, StringComparer.OrdinalIgnoreCase);
    }
}
