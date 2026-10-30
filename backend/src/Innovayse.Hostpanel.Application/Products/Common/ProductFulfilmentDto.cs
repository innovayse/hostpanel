using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>
/// The facts about a product that a billed line must keep a copy of, as the Products module answers them
/// to the modules that bill it.
/// </summary>
/// <param name="ProductId">The product's id.</param>
/// <param name="IsDigital">Whether the product is a digital one, delivered by fulfilment plugins instead of provisioned on a hosting account.</param>
/// <param name="Sku">The product's normalised SKU, or <see langword="null"/> when it has none.</param>
/// <param name="Metadata">The product's metadata, the JSON object text as the product holds it, or <see langword="null"/> when it has none.</param>
public sealed record ProductFulfilmentDto(int ProductId, bool IsDigital, string? Sku, string? Metadata)
{
    /// <summary>Copies these facts into the snapshot a billed line carries.</summary>
    /// <param name="billingCycle">The billing cycle the product is sold on in that line.</param>
    /// <returns>The snapshot for the invoice line.</returns>
    public ProductSnapshot ToSnapshot(BillingCycle billingCycle) =>
        new(ProductId, billingCycle, IsDigital, Sku, Metadata);
}
