using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.CountLiveServices;

/// <summary>Handles <see cref="CountLiveServicesQuery"/>: counts the services of each product that are not terminated.</summary>
public sealed class CountLiveServicesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CountLiveServicesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The products, and optionally the client and the order lines to leave out.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per requested product, 0 when it has no live service. Never refuses.</returns>
    public async Task<Result<IReadOnlyDictionary<int, int>>> HandleAsync(CountLiveServicesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var productIds = query.ProductIds.Distinct().ToList();
        if (productIds.Count == 0)
        {
            return Result<IReadOnlyDictionary<int, int>>.Success(new Dictionary<int, int>());
        }

        var services = _context.ClientServices
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId) && s.Status != ServiceStatus.Terminated);

        if (query.ClientId is { } clientId)
        {
            services = services.Where(s => s.ClientId == clientId);
        }

        if (query.ExcludedOrderItemIds is { Count: > 0 } excluded)
        {
            var excludedIds = excluded.ToList();
            services = services.Where(s => s.OrderItemId == null || !excludedIds.Contains(s.OrderItemId.Value));
        }

        var counted = await services
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, ct);

        IReadOnlyDictionary<int, int> answer = productIds.ToDictionary(id => id, id => counted.GetValueOrDefault(id));
        return Result<IReadOnlyDictionary<int, int>>.Success(answer);
    }
}
