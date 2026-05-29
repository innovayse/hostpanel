namespace Innovayse.Application.Orders.Commands.PlaceOrder;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.DTOs;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>
/// Places a new order for an existing or newly registered client.
/// Creates the <see cref="Order"/> aggregate, snapshots product prices into line items,
/// generates a linked <see cref="Invoice"/>, and persists everything in a single unit of work.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="productRepo">Product repository for price snapshot lookups.</param>
/// <param name="clientRepo">Client repository for client lookups.</param>
/// <param name="invoiceRepo">Invoice repository for creating the linked invoice.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="userService">Identity user management for guest checkout registration.</param>
public sealed class PlaceOrderHandler(
    IOrderRepository orderRepo,
    IProductRepository productRepo,
    IClientRepository clientRepo,
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow,
    IUserService userService)
{
    /// <summary>
    /// Handles <see cref="PlaceOrderCommand"/>.
    /// </summary>
    /// <param name="cmd">The place order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PlaceOrderResultDto"/> containing the new order and invoice IDs.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a guest registration fails, a client is not found,
    /// a product is not found or inactive, or an invalid billing cycle is specified.
    /// </exception>
    public async Task<PlaceOrderResultDto> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var clientId = await ResolveClientIdAsync(cmd, ct);

        var nextNumber = await orderRepo.GetNextOrderNumberAsync(ct);
        var orderNumber = $"ORD-{nextNumber:D4}";

        var productIds = cmd.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await productRepo.FindByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        var order = Order.Create(orderNumber, clientId, cmd.PaymentMethod, cmd.IpAddress);

        foreach (var item in cmd.Items)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product))
            {
                throw new InvalidOperationException($"Product {item.ProductId} not found.");
            }

            if (product.Status != ProductStatus.Active)
            {
                throw new InvalidOperationException($"Product {item.ProductId} is not available for ordering.");
            }

            var price = ResolvePrice(product, item.BillingCycle);
            order.AddItem(item.ProductId, product.Name, item.BillingCycle, price, price, item.Domain, item.Hostname);
        }

        orderRepo.Add(order);

        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7));

        foreach (var item in order.Items)
        {
            invoice.AddItem(item.ProductName, item.FirstPaymentAmount, 1);
        }

        invoiceRepo.Add(invoice);
        await uow.SaveChangesAsync(ct);

        order.LinkInvoice(invoice.Id);
        await uow.SaveChangesAsync(ct);

        return new PlaceOrderResultDto(order.Id, invoice.Id);
    }

    /// <summary>
    /// Resolves the client ID from the command. If <see cref="PlaceOrderCommand.ClientId"/>
    /// is provided, validates it exists. Otherwise creates a new Identity user and
    /// <see cref="Client"/> record for guest checkout.
    /// </summary>
    /// <param name="cmd">The place order command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved client ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client cannot be found or created.</exception>
    private async Task<int> ResolveClientIdAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        if (cmd.ClientId.HasValue)
        {
            var client = await clientRepo.FindByIdAsync(cmd.ClientId.Value, ct)
                ?? throw new InvalidOperationException($"Client {cmd.ClientId.Value} not found.");
            return client.Id;
        }

        var userId = await userService.CreateAsync(cmd.Email!, cmd.Password!, ct);
        await userService.AddToRoleAsync(userId, Roles.Client, ct);

        var newClient = Client.Create(userId, cmd.FirstName!, cmd.LastName!, cmd.Email!);
        clientRepo.Add(newClient);
        await uow.SaveChangesAsync(ct);

        return newClient.Id;
    }

    /// <summary>
    /// Resolves the correct price for a product based on the billing cycle.
    /// </summary>
    /// <param name="product">The product to price.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <returns>The resolved price.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the billing cycle is not recognised.</exception>
    private static decimal ResolvePrice(Product product, string billingCycle)
    {
        return billingCycle.ToLowerInvariant() switch
        {
            "monthly" => product.MonthlyPrice,
            "annual" or "annually" => product.AnnualPrice,
            _ => throw new InvalidOperationException($"Unsupported billing cycle: {billingCycle}.")
        };
    }
}
