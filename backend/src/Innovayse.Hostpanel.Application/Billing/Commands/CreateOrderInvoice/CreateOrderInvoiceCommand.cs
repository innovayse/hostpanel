using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;

/// <summary>
/// Raises the unpaid invoice for a newly placed order, in the currency the order was priced in,
/// due a week from now.
/// </summary>
/// <param name="ClientId">The ordering client.</param>
/// <param name="Currency">ISO 4217 alpha code the order was priced in; the invoice bills in it.</param>
/// <param name="Items">One line per order line, at its first payment amount, with the product snapshot it was sold as.</param>
public sealed record CreateOrderInvoiceCommand(int ClientId, string Currency, IReadOnlyList<OrderInvoiceItemDto> Items);
