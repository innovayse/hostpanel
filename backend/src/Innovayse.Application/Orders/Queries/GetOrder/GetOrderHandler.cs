namespace Innovayse.Application.Orders.Queries.GetOrder;

using Innovayse.Application.Orders.DTOs;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders.Interfaces;

/// <summary>
/// Loads a single order with all its items and maps it to <see cref="OrderDetailDto"/>.
/// Resolves the client display name and the linked invoice status.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="clientRepo">Client repository for client name lookup.</param>
/// <param name="invoiceRepo">Invoice repository for linked invoice status lookup.</param>
public sealed class GetOrderHandler(
    IOrderRepository orderRepo,
    IClientRepository clientRepo,
    IInvoiceRepository invoiceRepo)
{
    /// <summary>
    /// Handles <see cref="GetOrderQuery"/>.
    /// </summary>
    /// <param name="query">The get order query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="OrderDetailDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the order is not found.</exception>
    public async Task<OrderDetailDto> HandleAsync(GetOrderQuery query, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(query.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {query.OrderId} not found.");

        var client = await clientRepo.FindByIdAsync(order.ClientId, ct);
        var clientName = client is not null ? $"{client.FirstName} {client.LastName}" : "Unknown";

        string? invoiceStatus = null;
        if (order.InvoiceId.HasValue)
        {
            var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct);
            invoiceStatus = invoice?.Status.ToString();
        }

        var items = order.Items
            .Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.BillingCycle,
                i.Domain,
                i.Hostname,
                i.FirstPaymentAmount,
                i.RecurringAmount,
                i.Status.ToString()))
            .ToList();

        return new OrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.ClientId,
            clientName,
            order.Status.ToString(),
            order.PaymentMethod,
            order.Items.Sum(i => i.FirstPaymentAmount),
            order.InvoiceId,
            invoiceStatus,
            order.IpAddress,
            order.Notes,
            items,
            order.CreatedAt);
    }
}
