namespace Innovayse.Application.Domains.Commands.SyncTldCostPrices;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="SyncTldCostPricesCommand"/> by iterating all TLD configurations grouped
/// by registrar module, polling each provider for current pricing, updating cost prices
/// for matching TLDs, and re-scheduling itself for the next day at 04:00 UTC.
/// </summary>
/// <param name="repo">TLD configuration repository for loading and persisting configs.</param>
/// <param name="factory">Factory for resolving registrar provider implementations by module.</param>
/// <param name="uow">Unit of work for committing changes per registrar group.</param>
/// <param name="bus">Wolverine message bus for scheduling the next sync run.</param>
/// <param name="logger">Logger for recording sync progress and diagnostics.</param>
public sealed class SyncTldCostPricesHandler(
    ITldConfigRepository repo,
    IRegistrarProviderFactory factory,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<SyncTldCostPricesHandler> logger)
{
    /// <summary>
    /// Syncs cost prices for all TLD configurations from their respective registrar providers,
    /// then re-schedules the next run for tomorrow at 04:00 UTC.
    /// Individual registrar or TLD failures are logged and swallowed so one error does not abort the batch.
    /// </summary>
    /// <param name="cmd">The sync TLD cost prices command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(SyncTldCostPricesCommand cmd, CancellationToken ct)
    {
        var allConfigs = await repo.ListAllAsync(ct);

        var grouped = allConfigs.GroupBy(c => c.RegistrarModule);

        foreach (var group in grouped)
        {
            try
            {
                await SyncGroupAsync(group.Key, group.ToList(), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "SyncTldCostPrices: failed to sync module {Module}.", group.Key);
            }
        }

        // Re-schedule for tomorrow at 04:00 UTC.
        var nextRun = CalculateNextRun();
        await bus.ScheduleAsync(new SyncTldCostPricesCommand(), nextRun);
        logger.LogDebug("SyncTldCostPrices: next run scheduled for {Next:u}.", nextRun);
    }

    /// <summary>
    /// Syncs cost prices for a single registrar module group by fetching pricing from
    /// the provider and updating matching TLD configurations.
    /// </summary>
    /// <param name="module">The registrar module whose provider to query.</param>
    /// <param name="configs">TLD configurations belonging to this registrar module.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SyncGroupAsync(RegistrarModule module, List<TldConfig> configs, CancellationToken ct)
    {
        var provider = factory.GetProvider(module);
        var pricingList = await provider.GetTldPricingAsync(ct);

        var pricingLookup = pricingList.ToDictionary(p => p.Tld.ToLower().Trim(), p => p);

        var updatedCount = 0;

        foreach (var config in configs)
        {
            if (pricingLookup.TryGetValue(config.Tld, out var pricing))
            {
                config.UpdateCostPrices(pricing.Register, pricing.Transfer, pricing.Renew);
                config.MarkSynced();
                updatedCount++;

                logger.LogDebug(
                    "SyncTldCostPrices: updated cost prices for TLD {Tld} from {Module}.",
                    config.Tld, module);
            }
        }

        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "SyncTldCostPrices: synced {Updated}/{Total} TLD(s) for {Module}.",
            updatedCount, configs.Count, module);
    }

    /// <summary>
    /// Calculates the next scheduled run time as tomorrow at 04:00 UTC.
    /// If the current time is before 04:00 UTC today, schedules for today at 04:00 UTC.
    /// </summary>
    /// <returns>The next run <see cref="DateTimeOffset"/> at 04:00 UTC.</returns>
    private static DateTimeOffset CalculateNextRun()
    {
        var now = DateTimeOffset.UtcNow;
        var todayAt4 = new DateTimeOffset(now.Date.AddHours(4), TimeSpan.Zero);

        return now < todayAt4 ? todayAt4 : todayAt4.AddDays(1);
    }
}
