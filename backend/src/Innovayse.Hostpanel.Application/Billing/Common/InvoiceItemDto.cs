namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>DTO representing a single line item on an invoice.</summary>
/// <param name="Id">Line item primary key.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="Amount">Line total (UnitPrice × Quantity).</param>
/// <param name="DiscountPercent">
/// The client group discount in percent already taken off the list price to reach <c>UnitPrice</c>, as it was when the
/// line was billed; 0 for a line without one.
/// </param>
/// <param name="Sku">The SKU of the product the line was sold from, read from the line's product snapshot; null for a line without one (a domain, a manual line).</param>
public sealed record InvoiceItemDto(
    int Id, string Description, decimal UnitPrice, int Quantity, decimal Amount, decimal DiscountPercent, string? Sku);
