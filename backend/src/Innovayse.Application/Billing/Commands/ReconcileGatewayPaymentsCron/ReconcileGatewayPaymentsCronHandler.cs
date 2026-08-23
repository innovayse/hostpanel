namespace Innovayse.Application.Billing.Commands.ReconcileGatewayPaymentsCron;

using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="ReconcileGatewayPaymentsCronCommand"/>: verifies every unpaid
/// invoice whose gateway session started between 25 minutes and 24 hours ago
/// (younger sessions may still be on the payment page — Inecobank sessions last
/// 20 minutes; older ones are abandoned and left for audit), then reschedules itself.
/// </summary>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReconcileGatewayPaymentsCronHandler(
    IInvoiceRepository invoiceRepo,
    IMessageBus bus,
    ILogger<ReconcileGatewayPaymentsCronHandler> logger)
{
    /// <summary>Minutes between runs.</summary>
    public const int IntervalMinutes = 5;

    /// <summary>Handles the cron command.</summary>
    /// <param name="cmd">The cron command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all pending sessions were checked.</returns>
    public async Task HandleAsync(ReconcileGatewayPaymentsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var now = DateTimeOffset.UtcNow;
        var pending = await invoiceRepo.ListPendingGatewayPaymentsAsync(
            startedAfter: now.AddHours(-24), startedBefore: now.AddMinutes(-25), ct);

        logger.LogInformation("Gateway payment reconciliation: {Count} pending session(s).", pending.Count);

        foreach (var invoice in pending)
        {
            try
            {
                var result = await bus.InvokeAsync<string>(new CompleteGatewayPaymentCommand(invoice.Id), ct);
                if (result == "paid")
                {
                    logger.LogWarning(
                        "Reconciler recovered a paid-but-unreturned payment for invoice {InvoiceId}.", invoice.Id);
                }
            }
            catch (Exception ex)
            {
                // One bad session must not starve the rest; it will be retried next run.
                logger.LogError(ex, "Reconciliation failed for invoice {InvoiceId}.", invoice.Id);
            }
        }

        await bus.ScheduleAsync(new ReconcileGatewayPaymentsCronCommand(), now.AddMinutes(IntervalMinutes));
    }
}
