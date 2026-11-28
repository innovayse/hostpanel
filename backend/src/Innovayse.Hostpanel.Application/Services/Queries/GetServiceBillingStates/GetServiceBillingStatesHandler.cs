using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceBillingStates;

/// <summary>Handles <see cref="GetServiceBillingStatesQuery"/> with one read.</summary>
public sealed class GetServiceBillingStatesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetServiceBillingStatesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Reads the states.</summary>
    /// <param name="query">The services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One state per existing service, ordered by id. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ServiceBillingStateDto>>> HandleAsync(GetServiceBillingStatesQuery query, CancellationToken ct)
    {
        if (query.ServiceIds.Count == 0)
        {
            return Result<IReadOnlyList<ServiceBillingStateDto>>.Success([]);
        }

        var states = await _context.ClientServices.AsNoTracking()
            .Where(s => query.ServiceIds.Contains(s.Id))
            .OrderBy(s => s.Id)
            .Select(s => new ServiceBillingStateDto(s.Id, s.Status, s.SuspensionSource, s.OverrideAutoSuspend, s.SuspendUntil, s.AutoRenew,
                s.TrialStatus == TrialStatus.Active || s.TrialStatus == TrialStatus.AwaitingPayment))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ServiceBillingStateDto>>.Success(states);
    }
}
