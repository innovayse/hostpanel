using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One line of the invoice raised for an order, submitted with
/// <see cref="Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice.CreateOrderInvoiceCommand"/>.
/// Unlike <see cref="CreateInvoiceItemDto"/> it carries the product snapshot, which only the order flow
/// may set: the snapshot is copied from the catalogue by the Orders module and is never taken from a request body.
/// </summary>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit (≥ 0), after the discount.</param>
/// <param name="Quantity">Number of units (≥ 1).</param>
/// <param name="ProductSnapshot">What the product the line charges for was when it was ordered, or <see langword="null"/> on a domain line.</param>
/// <param name="DiscountPercent">
/// The client group discount in percent already taken off the list price to reach <paramref name="UnitPrice"/>;
/// 0 when none was given.
/// </param>
public sealed record OrderInvoiceItemDto(
    string Description, decimal UnitPrice, int Quantity, ProductSnapshot? ProductSnapshot, decimal DiscountPercent);
