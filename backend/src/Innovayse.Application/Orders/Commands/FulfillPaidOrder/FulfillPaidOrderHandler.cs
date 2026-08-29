namespace Innovayse.Application.Orders.Commands.FulfillPaidOrder;

using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.RegisterDomain;
using Innovayse.Application.Domains.Commands.TransferDomain;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="FulfillPaidOrderCommand"/>: accepts the paid order, dispatches
/// domain registration/transfer or service creation per line item, auto-links a domain
/// to a hosting service bought in the same order, and issues an automatic refund via
/// the invoice's payment gateway when the registrar immediately rejects a domain.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="invoiceRepo">Invoice repository (for the auto-refund path).</param>
/// <param name="stripeService">Stripe service used when the invoice was paid via Stripe.</param>
/// <param name="pluginResolver">Resolver for hosted-gateway payment plugins.</param>
/// <param name="domainRepo">Domain repository for auto-linking.</param>
/// <param name="uow">Unit of work.</param>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="logger">Structured logger.</param>
public sealed class FulfillPaidOrderHandler(
    IOrderRepository orderRepo,
    IInvoiceRepository invoiceRepo,
    IStripeService stripeService,
    IPaymentPluginResolver pluginResolver,
    IDomainRepository domainRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<FulfillPaidOrderHandler> logger)
{
    /// <summary>Handles <see cref="FulfillPaidOrderCommand"/>.</summary>
    /// <param name="cmd">The fulfill command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when fulfillment is dispatched.</returns>
    public async Task HandleAsync(FulfillPaidOrderCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

        if (order.Status != OrderStatus.Pending)
        {
            logger.LogInformation(
                "Order {OrderId} is {Status}; fulfillment already ran — skipping.", order.Id, order.Status);
            return;
        }

        order.Accept();
        await uow.SaveChangesAsync(ct);

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {order.Id} has no linked invoice.");
        }

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        int? createdDomainId = null;
        int? createdServiceId = null;
        string? orderDomainName = null;

        foreach (var item in order.Items)
        {
            if (item.DomainAction is not null)
            {
                // Domain-action items must carry a domain name; a null here means the order
                // was built incorrectly upstream. Fail loudly and name the offending item
                // rather than let it surface as a bare NullReferenceException.
                var domain = item.Domain
                    ?? throw new InvalidOperationException(
                        $"Order {order.Id} item {item.Id} has domain action '{item.DomainAction}' " +
                        "but no domain name.");

                try
                {
                    if (item.DomainAction == "register")
                    {
                        createdDomainId = await bus.InvokeAsync<int>(
                            new RegisterDomainCommand(
                                order.ClientId,
                                domain,
                                item.Years ?? 1,
                                WhoisPrivacy: false,
                                AutoRenew: true,
                                Nameserver1: null,
                                Nameserver2: null,
                                FirstPaymentAmount: item.FirstPaymentAmount,
                                RecurringAmount: item.RecurringAmount,
                                PaymentMethod: order.PaymentMethod), ct);
                    }
                    else if (item.DomainAction == "transfer")
                    {
                        var eppCode = item.EppCode
                            ?? throw new InvalidOperationException(
                                $"Order {order.Id} item {item.Id} is a transfer for '{domain}' " +
                                "but has no EPP code.");

                        createdDomainId = await bus.InvokeAsync<int>(
                            new TransferDomainCommand(
                                order.ClientId,
                                domain,
                                eppCode,
                                WhoisPrivacy: false,
                                FirstPaymentAmount: item.FirstPaymentAmount,
                                RecurringAmount: item.RecurringAmount,
                                PaymentMethod: order.PaymentMethod), ct);
                    }

                    orderDomainName = domain;
                }
                catch (InvalidOperationException ex)
                {
                    // Registrar immediately rejected the order (duplicate domain, invalid TLD,
                    // API error, etc.). Issue automatic refund and notify.
                    await HandleDomainRegistrationFailedAsync(invoice, order.ClientId, domain, ex.Message, ct);
                }
            }
            else
            {
                try
                {
                    createdServiceId = await bus.InvokeAsync<int>(
                        new OrderServiceCommand(
                            order.ClientId, item.ProductId, item.BillingCycle,
                            item.FirstPaymentAmount, item.RecurringAmount,
                            order.PaymentMethod, item.Domain ?? orderDomainName), ct);
                }
                catch (Exception ex)
                {
                    // A paid order with an unprovisioned item is a critical, must-be-found
                    // condition: the client was charged and the order is Accepted, but this
                    // item never became a service, and every later fulfillment retry is a
                    // logged no-op (order.Status is no longer Pending). One failing item must
                    // not abort the rest of the loop, so log loud and keep going.
                    logger.LogCritical(
                        ex,
                        "Service provisioning failed for order {OrderId} item {ItemId} (product {ProductId}). " +
                        "Order is Paid/Accepted but this item was never provisioned — manual intervention required.",
                        order.Id, item.Id, item.ProductId);
                }
            }
        }

        // Auto-link domain to hosting service when both in same order
        if (createdDomainId.HasValue && createdServiceId.HasValue)
        {
            var domain = await domainRepo.FindByIdAsync(createdDomainId.Value, ct);
            domain?.LinkService(createdServiceId.Value);
            await uow.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Issues an automatic refund through whichever gateway the invoice was paid with,
    /// records it, and publishes <see cref="DomainRegistrationFailedEvent"/>.
    /// </summary>
    /// <param name="invoice">The invoice that was paid and must be refunded.</param>
    /// <param name="clientId">The client who placed the order.</param>
    /// <param name="domainName">The domain name that failed to register.</param>
    /// <param name="reason">Human-readable reason from the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleDomainRegistrationFailedAsync(
        Invoice invoice, int clientId, string domainName, string reason, CancellationToken ct)
    {
        logger.LogError(
            "Domain registration failed for '{DomainName}' (client {ClientId}): {Reason}. Issuing refund.",
            domainName, clientId, reason);

        try
        {
            string refundId;
            string gateway;
            if (invoice.GatewayModule is not null && invoice.GatewayOrderId is not null)
            {
                var plugin = await pluginResolver.ResolveAsync(invoice.GatewayModule, ct)
                    ?? throw new InvalidOperationException(
                        $"Payment plugin '{invoice.GatewayModule}' is not available for the refund.");
                var amountMinor = CurrencyCodes.ToMinorUnits(invoice.Total);
                refundId = await plugin.RefundAsync(invoice.GatewayOrderId, amountMinor, ct);
                gateway = invoice.GatewayModule;
            }
            else
            {
                var gatewayTransactionId = invoice.GatewayTransactionId
                    ?? throw new InvalidOperationException(
                        $"Invoice {invoice.Id} has no gateway transaction id to refund via Stripe.");
                refundId = await stripeService.RefundAsync(gatewayTransactionId, ct);
                gateway = "stripe";
            }

            invoice.AddRefund(
                DateTimeOffset.UtcNow,
                gateway: gateway,
                transactionId: refundId,
                amount: invoice.Total,
                fees: 0,
                notes: $"Automatic refund: domain registration failed for '{domainName}'. Reason: {reason}");
            await uow.SaveChangesAsync(ct);

            await bus.PublishAsync(new DomainRegistrationFailedEvent(clientId, domainName, invoice.Id, reason));
        }
        catch (Exception refundEx)
        {
            // Refund failed — log at critical level so it gets immediate attention.
            // The domain was never created so no cleanup needed there,
            // but the client was charged and could not be refunded automatically.
            logger.LogCritical(
                refundEx,
                "REFUND FAILED for domain '{DomainName}' (client {ClientId}, invoice {InvoiceId}). " +
                "Manual refund required. Original failure: {Reason}",
                domainName, clientId, invoice.Id, reason);
        }
    }
}
