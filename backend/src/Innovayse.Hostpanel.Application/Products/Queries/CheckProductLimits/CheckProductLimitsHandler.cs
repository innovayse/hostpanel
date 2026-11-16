using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Products.Queries.CheckProductLimits;

/// <summary>
/// Handles <see cref="CheckProductLimitsQuery"/>: the one place a product's cap on live services and its one-per-client
/// rule are enforced, for the order flow and for a service a client orders for itself. The count includes open orders
/// not yet fulfilled, so two unpaid orders cannot both take the last place.
/// </summary>
/// <remarks>
/// The answer is only as good as the moment it is read: the caller holds the product's lock
/// (<see cref="Extensions.ProductLimitLockExtensions.LockProductPlacesAsync"/>) from before this check until the service
/// or order that takes the place is committed, so a parallel caller waits and then counts it.
/// </remarks>
public sealed class CheckProductLimitsHandler
{
    /// <summary>The module's data context, the limits are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the Orders module's count of taken places is asked on.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, the limits are read from.</param>
    /// <param name="bus">The bus the Orders module's count of taken places is asked on.</param>
    public CheckProductLimitsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The products of the services about to be created, and the client they are for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>ORDER_PRODUCT_SOLD_OUT</c> when a product's cap would be passed, <c>ORDER_PRODUCT_ONE_PER_CLIENT</c>
    /// when the client already holds or has ordered a one-per-client product or the request names it twice, or the
    /// count's own failure.
    /// </returns>
    public async Task<Result> HandleAsync(CheckProductLimitsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wanted = query.ProductIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var ids = wanted.Keys.ToList();
        var limited = await _context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && (p.MaxActiveServices != null || p.OnePerClient))
            .Select(p => new { p.Id, p.MaxActiveServices, p.OnePerClient })
            .ToListAsync(ct);
        if (limited.Count == 0)
        {
            return Result.Success();
        }

        var onePerClient = limited.Where(p => p.OnePerClient).ToList();
        if (onePerClient.Any(p => wanted[p.Id] > 1))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
        }

        if (onePerClient.Count > 0 && query.ClientId is { } clientId)
        {
            var held = await _bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
                new CountProductCommitmentsQuery(onePerClient.Select(p => p.Id).ToList(), clientId), ct);
            if (held.IsFailure)
            {
                return Result.FailureCode(held.ErrorCode!);
            }

            if (onePerClient.Any(p => held.Value!.GetValueOrDefault(p.Id) > 0))
            {
                return Result.FailureCode(HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
            }
        }

        var capped = limited.Where(p => p.MaxActiveServices is not null).ToList();
        if (capped.Count > 0)
        {
            var taken = await _bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
                new CountProductCommitmentsQuery(capped.Select(p => p.Id).ToList()), ct);
            if (taken.IsFailure)
            {
                return Result.FailureCode(taken.ErrorCode!);
            }

            if (capped.Any(p => taken.Value!.GetValueOrDefault(p.Id) + wanted[p.Id] > p.MaxActiveServices))
            {
                return Result.FailureCode(HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
            }
        }

        return Result.Success();
    }
}
