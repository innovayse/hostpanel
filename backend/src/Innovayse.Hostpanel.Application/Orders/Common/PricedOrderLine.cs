using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>
/// One checkout line after it was checked and priced, held until every line is known good so
/// nothing is created for an order that would be refused.
/// </summary>
/// <param name="Item">The line as the checkout sent it.</param>
/// <param name="ProductName">The product's name, snapshotted on the order line.</param>
/// <param name="Price">The line's price in the payer's currency.</param>
/// <param name="Snapshot">
/// What the product is at ordering time, carried onto the line of the order's invoice; <see langword="null"/> on a
/// domain line, which charges for a domain and not for a catalogue product.
/// </param>
public sealed record PricedOrderLine(PlaceOrderItemDto Item, string ProductName, decimal Price, ProductSnapshot? Snapshot);
