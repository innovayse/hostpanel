namespace Innovayse.Application.Billing.Commands.CompleteGatewayPayment;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="CompleteGatewayPaymentCommand"/>. Called from the payer's
/// return page and from the reconciliation job — both paths converge here, so
/// idempotency is load-bearing.
/// </summary>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="orderRepo">Order repository (to fulfill the linked order).</param>
/// <param name="pluginResolver">Payment plugin resolver.</param>
/// <param name="uow">Unit of work.</param>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="logger">Structured logger.</param>
public sealed class CompleteGatewayPaymentHandler(
    IInvoiceRepository invoiceRepo,
    IOrderRepository orderRepo,
    IPaymentPluginResolver pluginResolver,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<CompleteGatewayPaymentHandler> logger)
{
    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The complete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome of the completion attempt.</returns>
    /// <exception cref="InvalidOperationException">Invoice not found, no session, or plugin unavailable.</exception>
    public async Task<GatewayCompletionState> HandleAsync(CompleteGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await invoiceRepo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Refunded)
        {
            return GatewayCompletionState.Paid;
        }

        if (invoice.GatewayModule is null || invoice.GatewayOrderId is null)
        {
            throw new InvalidOperationException($"Invoice {cmd.InvoiceId} has no gateway payment session.");
        }

        var plugin = await pluginResolver.ResolveAsync(invoice.GatewayModule, ct)
            ?? throw new InvalidOperationException($"Payment method '{invoice.GatewayModule}' is not available.");

        var status = await plugin.GetStatusAsync(invoice.GatewayOrderId, ct);

        if (status.State != GatewayPaymentState.Paid)
        {
            logger.LogInformation(
                "Invoice {InvoiceId} gateway session {GatewayOrderId} is {State} ({Raw}).",
                invoice.Id, invoice.GatewayOrderId, status.State, status.RawStatus);
            return status.State == GatewayPaymentState.Pending
                ? GatewayCompletionState.Pending
                : GatewayCompletionState.Declined;
        }

        invoice.MarkPaidViaGateway(status.TransactionId ?? invoice.GatewayOrderId);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another writer (the result-page auto-check racing the reconciler, or a retried
            // click) already completed this same payment between our read and our write. The
            // invoice's own xmin concurrency token caught the race — re-read the current state
            // rather than clobbering it, and treat "already paid" as success, not failure.
            //
            // The failed SaveChangesAsync left `invoice` tracked by the DbContext in Modified
            // state, still carrying the in-memory Status = Paid that MarkPaidViaGateway set
            // above (that write never reached the database). EF's identity resolution means a
            // plain re-read via invoiceRepo.FindByIdAsync would return that same tracked
            // instance instead of querying the database, so `current?.Status` would read Paid
            // unconditionally and this branch would treat every concurrency conflict — not just
            // the one we intend to absorb — as "already paid by someone else". DetachAll() drops
            // the stale tracked entities first so the re-read is forced to hit the database and
            // reflect what was actually persisted by the other writer.
            uow.DetachAll();
            var current = await invoiceRepo.FindByIdAsync(cmd.InvoiceId, ct);
            if (current?.Status is InvoiceStatus.Paid or InvoiceStatus.Refunded)
            {
                logger.LogInformation(
                    "Invoice {InvoiceId} was already marked paid by a concurrent completion.", cmd.InvoiceId);
                return GatewayCompletionState.Paid;
            }

            throw;
        }

        var order = await orderRepo.FindByInvoiceIdAsync(invoice.Id, ct);
        if (order is not null)
        {
            await bus.InvokeAsync(new FulfillPaidOrderCommand(order.Id), ct);
        }

        return GatewayCompletionState.Paid;
    }
}
