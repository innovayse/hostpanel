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

    /// <summary>
    /// Minutes a gateway payment session must have been running before it is checked.
    /// Inecobank sessions last 20 minutes, so anything younger may still be on the
    /// payment page; 25 minutes leaves a safety margin past that.
    /// </summary>
    private const int SessionSettleMinutes = 25;

    /// <summary>
    /// Hours after which an unpaid gateway session is treated as abandoned and left
    /// out of reconciliation for audit rather than checked forever.
    /// </summary>
    private const int AbandonedAfterHours = 24;

    /// <summary>Handles the cron command.</summary>
    /// <param name="cmd">The cron command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all pending sessions were checked.</returns>
    public async Task HandleAsync(ReconcileGatewayPaymentsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var now = DateTimeOffset.UtcNow;

        try
        {
            var pending = await invoiceRepo.ListPendingGatewayPaymentsAsync(
                startedAfter: now.AddHours(-AbandonedAfterHours),
                startedBefore: now.AddMinutes(-SessionSettleMinutes),
                ct);

            logger.LogInformation("Gateway payment reconciliation: {Count} pending session(s).", pending.Count);

            foreach (var invoice in pending)
            {
                try
                {
                    var result = await bus.InvokeAsync<GatewayCompletionState>(
                        new CompleteGatewayPaymentCommand(invoice.Id), ct);
                    if (result == GatewayCompletionState.Paid)
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
        }
        catch (Exception ex)
        {
            // The reconciler is the only safety net for the whole no-webhook payment design —
            // if the query itself throws (DB blip, etc.), the reschedule below in `finally`
            // must still run, or every unpaid gateway session past this point silently stops
            // being checked until the next API restart.
            logger.LogError(ex, "Gateway payment reconciliation run failed.");
        }
        finally
        {
            await bus.ScheduleAsync(new ReconcileGatewayPaymentsCronCommand(), now.AddMinutes(IntervalMinutes));
        }
    }
}
