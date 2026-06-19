namespace Innovayse.Application.Billing.Commands.ProcessRenewalsCron;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="ProcessRenewalsCronCommand"/> by creating renewal invoices for all
/// active services whose next renewal date has arrived.
/// Groups services by client and creates one invoice per client.
/// Re-schedules itself for the next day after completion.
/// </summary>
public sealed class ProcessRenewalsCronHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<ProcessRenewalsCronHandler> logger)
{
    /// <summary>Hour (UTC) at which this job runs daily.</summary>
    private const int CronHour = 5;

    /// <summary>Default payment term in days for renewal invoices.</summary>
    private const int PaymentTermDays = 14;

    /// <summary>
    /// Queries all active services due for renewal, creates invoices grouped by client,
    /// and advances each service's renewal date to the next cycle.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ProcessRenewalsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var dueServices = await serviceRepo.ListDueForRenewalAsync(DateTimeOffset.UtcNow, ct);

        if (dueServices.Count > 0)
        {
            var productIds = dueServices.Select(s => s.ProductId).Distinct();
            var products = await productRepo.FindByIdsAsync(productIds, ct);
            var productMap = products.ToDictionary(p => p.Id);

            var grouped = dueServices.GroupBy(s => s.ClientId);

            foreach (var group in grouped)
            {
                var clientId = group.Key;
                var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(PaymentTermDays));

                foreach (var service in group)
                {
                    var productName = productMap.TryGetValue(service.ProductId, out var product)
                        ? product.Name
                        : $"Service #{service.Id}";

                    invoice.AddItem($"Renewal: {productName}", service.RecurringAmount, 1);
                    service.AdvanceRenewal();
                }

                invoiceRepo.Add(invoice);
                await uow.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Created renewal invoice {InvoiceId} for client {ClientId} with {Count} services.",
                    invoice.Id, clientId, group.Count());
            }
        }

        logger.LogInformation(
            "ProcessRenewalsCron processed {Count} services due for renewal.", dueServices.Count);

        // Re-schedule for next day
        var nextRun = NextDailyOccurrence(CronHour);
        await bus.ScheduleAsync(new ProcessRenewalsCronCommand(), nextRun);
        logger.LogInformation("ProcessRenewalsCronCommand re-scheduled for {NextRun:u}.", nextRun);
    }

    /// <summary>
    /// Calculates the next UTC occurrence of the given hour.
    /// If the hour has already passed today, returns the same hour tomorrow.
    /// </summary>
    /// <param name="hour">Target hour in UTC (0-23).</param>
    /// <returns>The next occurrence of the specified hour in UTC.</returns>
    private static DateTimeOffset NextDailyOccurrence(int hour)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        return candidate > now ? candidate : candidate.AddDays(1);
    }
}
