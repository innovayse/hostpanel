namespace Innovayse.Application.Orders.Commands.ConfirmOrderPayment;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.RegisterDomain;
using Innovayse.Application.Domains.Commands.TransferDomain;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Confirms a Stripe payment for an order: verifies the PaymentIntent succeeded,
/// marks the linked invoice as paid, accepts the order, and dispatches
/// domain registration/transfer or service creation commands for each line item.
/// When both a domain and hosting are in the same order, auto-links them.
/// If the registrar immediately rejects a domain order, issues an automatic Stripe refund,
/// marks the invoice as refunded, and publishes <see cref="DomainRegistrationFailedEvent"/>.
/// </summary>
/// <param name="orderRepo">Order repository.</param>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="stripeService">Stripe payment service.</param>
/// <param name="domainRepo">Domain repository for auto-linking.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="bus">Wolverine message bus for dispatching service creation commands.</param>
/// <param name="logger">Logger for structured diagnostics.</param>
public sealed class ConfirmOrderPaymentHandler(
    IOrderRepository orderRepo,
    IInvoiceRepository invoiceRepo,
    IStripeService stripeService,
    IDomainRepository domainRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<ConfirmOrderPaymentHandler> logger)
{
    /// <summary>
    /// Handles <see cref="ConfirmOrderPaymentCommand"/>.
    /// </summary>
    /// <param name="cmd">The confirm order payment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the payment is confirmed and services are dispatched.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the order is not found, has no linked invoice, or the payment verification fails.
    /// </exception>
    public async Task HandleAsync(ConfirmOrderPaymentCommand cmd, CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(cmd.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {cmd.OrderId} not found.");

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {cmd.OrderId} has no linked invoice.");
        }

        var (success, transactionId) = await stripeService.VerifyPaymentIntentAsync(cmd.PaymentIntentId, ct);

        if (!success)
        {
            throw new InvalidOperationException(
                $"Payment verification failed for PaymentIntent {cmd.PaymentIntentId}.");
        }

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        invoice.MarkPaid(transactionId!);
        order.Accept();

        await uow.SaveChangesAsync(ct);

        int? createdDomainId = null;
        int? createdServiceId = null;
        string? orderDomainName = null;

        foreach (var item in order.Items)
        {
            if (item.DomainAction is not null)
            {
                try
                {
                    if (item.DomainAction == "register")
                    {
                        createdDomainId = await bus.InvokeAsync<int>(
                            new RegisterDomainCommand(
                                order.ClientId,
                                item.Domain!,
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
                        createdDomainId = await bus.InvokeAsync<int>(
                            new TransferDomainCommand(
                                order.ClientId,
                                item.Domain!,
                                item.EppCode!,
                                WhoisPrivacy: false,
                                FirstPaymentAmount: item.FirstPaymentAmount,
                                RecurringAmount: item.RecurringAmount,
                                PaymentMethod: order.PaymentMethod), ct);
                    }

                    orderDomainName = item.Domain;
                }
                catch (InvalidOperationException ex)
                {
                    // Registrar immediately rejected the order (duplicate domain, invalid TLD,
                    // API error, etc.). Issue automatic refund and notify.
                    await HandleDomainRegistrationFailedAsync(invoice, order.ClientId, item.Domain!, ex.Message, ct);
                }
            }
            else
            {
                createdServiceId = await bus.InvokeAsync<int>(
                    new OrderServiceCommand(
                        order.ClientId, item.ProductId, item.BillingCycle,
                        item.FirstPaymentAmount, item.RecurringAmount,
                        order.PaymentMethod, item.Domain ?? orderDomainName), ct);
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
    /// Handles an immediate domain registration failure by issuing a Stripe refund,
    /// recording the refund on the invoice, and publishing a failure event for notifications.
    /// </summary>
    /// <param name="invoice">The invoice that was paid and must be refunded.</param>
    /// <param name="clientId">The client who placed the order.</param>
    /// <param name="domainName">The domain name that failed to register.</param>
    /// <param name="reason">Human-readable reason from the registrar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleDomainRegistrationFailedAsync(
        Innovayse.Domain.Billing.Invoice invoice,
        int clientId,
        string domainName,
        string reason,
        CancellationToken ct)
    {
        logger.LogError(
            "Domain registration failed for '{DomainName}' (client {ClientId}): {Reason}. Issuing refund.",
            domainName, clientId, reason);

        try
        {
            var refundId = await stripeService.RefundAsync(invoice.GatewayTransactionId!, ct);

            invoice.AddRefund(
                DateTimeOffset.UtcNow,
                gateway: "stripe",
                transactionId: refundId,
                amount: invoice.Total,
                fees: 0,
                notes: $"Automatic refund: domain registration failed for '{domainName}'. Reason: {reason}");

            await uow.SaveChangesAsync(ct);

            await bus.PublishAsync(new DomainRegistrationFailedEvent(
                clientId, domainName, invoice.Id, reason));
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
