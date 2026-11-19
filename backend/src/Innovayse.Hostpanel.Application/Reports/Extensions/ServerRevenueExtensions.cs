using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Extensions;

/// <summary>
/// Reads what the active services on each server renew for: the one source of the Server Revenue Forecasts report and of
/// the revenue the admin Servers list shows on each server.
/// </summary>
public static class ServerRevenueExtensions
{
    /// <summary>
    /// Sums the active services provisioned on servers, converted to the base currency and spread over the months of their
    /// billing cycles. The database sums per server, currency and cycle, so only those few rows come back to be converted.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <param name="serverIds">The servers to read, or <see langword="null"/> for every server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The revenue of each server that has an active service, by server id; a server without one is absent.</returns>
    public static async Task<IReadOnlyDictionary<int, ServerRevenue>> GetServerRevenueAsync(
        this IHostpanelDbContext context, BaseCurrencyRates rates, IReadOnlyCollection<int>? serverIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rates);

        var services = context.ClientServices.AsNoTracking()
            .Where(s => s.Status == ServiceStatus.Active && s.ServerId != null);
        if (serverIds is not null)
        {
            services = services.Where(s => serverIds.Contains(s.ServerId!.Value));
        }

        var groups = await services
            .Join(context.Clients, s => s.ClientId, c => c.Id, (s, c) => new { ServerId = s.ServerId!.Value, c.Currency, s.BillingCycle, s.RecurringAmount })
            .GroupBy(x => new { x.ServerId, x.Currency, x.BillingCycle })
            .Select(g => new { g.Key.ServerId, g.Key.Currency, g.Key.BillingCycle, Amount = g.Sum(x => x.RecurringAmount), Services = g.Count() })
            .ToListAsync(ct);

        return groups
            .GroupBy(g => g.ServerId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var sums = g.Select(x => new ServiceRecurringSum(x.Currency, x.BillingCycle, x.Amount, x.Services)).ToList();
                    return new ServerRevenue(sums.Sum(s => s.Services), sums.MonthlyInBase(rates));
                });
    }
}
