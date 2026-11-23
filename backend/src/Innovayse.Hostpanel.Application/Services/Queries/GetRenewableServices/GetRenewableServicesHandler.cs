using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetRenewableServices;

/// <summary>
/// Handles <see cref="GetRenewableServicesQuery"/>: one read of the due services, each flagged with
/// whether an open cancellation request covers it.
/// </summary>
public sealed class GetRenewableServicesHandler
{
    /// <summary>The module's data context, for services and their cancellation requests.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for services and their cancellation requests.</param>
    public GetRenewableServicesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Lists the due services.</summary>
    /// <param name="query">The client and the window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The due services ordered by id. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<RenewableServiceDto>>> HandleAsync(GetRenewableServicesQuery query, CancellationToken ct)
    {
        var services = _context.ClientServices.AsNoTracking().NotInTrial()
            .Where(s => s.Status == ServiceStatus.Active && s.AutoRenew
                        && s.NextRenewalAt != null && s.NextRenewalAt <= query.DueBy);
        if (query.ClientId is { } clientId)
        {
            services = services.Where(s => s.ClientId == clientId);
        }

        var due = await services
            .OrderBy(s => s.Id)
            .Select(s => new RenewableServiceDto(
                s.Id,
                s.ClientId,
                s.ProductId,
                s.BillingCycle,
                s.CreatedAt.Day,
                s.NextRenewalAt!.Value,
                s.RecurringAmount,
                s.AutoTerminateEndOfCycle,
                _context.CancellationRequests.Any(r => r.ServiceId == s.Id && r.Status == CancellationStatus.Open)))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RenewableServiceDto>>.Success(due);
    }
}
