using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// What a product was at the moment it was billed, kept on the <see cref="InvoiceItem"/> that charges for it.
/// Plain values copied from the catalogue by the caller, never a reference to the product row.
/// </summary>
/// <remarks>
/// Fulfilment plugins are told about a paid, refunded or lapsed invoice long after it was raised, and the
/// catalogue may have changed by then: a SKU renamed, metadata edited, the product turned digital or deleted.
/// What they deliver must follow what was sold, so the line carries its own copy and nothing re-reads the
/// catalogue for it.
/// </remarks>
/// <param name="ProductId">Id of the product the line charges for, at billing time. Index only; the product row may be gone since.</param>
/// <param name="BillingCycle">The billing cycle the product was sold on.</param>
/// <param name="IsDigital">Whether the product was a digital one, delivered by fulfilment plugins instead of provisioned on a hosting account.</param>
/// <param name="Sku">The product's normalised SKU at billing time, or <see langword="null"/> when it had none.</param>
/// <param name="Metadata">The product's metadata at billing time, the JSON object text exactly as the product held it, or <see langword="null"/> when it had none.</param>
public sealed record ProductSnapshot(
    int ProductId,
    BillingCycle BillingCycle,
    bool IsDigital,
    string? Sku,
    string? Metadata);
