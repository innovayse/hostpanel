using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetTrialConversionService;

/// <summary>
/// Handles <see cref="GetTrialConversionServiceQuery"/>: one read of the service, answered only while its trial
/// is <see cref="TrialStatus.AwaitingPayment"/>, the service is Active and its paid period has a start.
/// </summary>
public sealed class GetTrialConversionServiceHandler
{
    /// <summary>The module's data context, for services and their cancellation requests.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for services and their cancellation requests.</param>
    public GetTrialConversionServiceHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Reads the converted trial.</summary>
    /// <param name="query">The service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The service, its period anchored on the day the paid period starts; <see langword="null"/> when no
    /// service awaits the payment of its conversion. Never refuses.
    /// </returns>
    public async Task<Result<RenewableServiceDto?>> HandleAsync(GetTrialConversionServiceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var service = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Id == query.ServiceId
                        && s.TrialStatus == TrialStatus.AwaitingPayment
                        && s.Status == ServiceStatus.Active
                        && s.NextRenewalAt != null)
            .Select(s => new RenewableServiceDto(
                s.Id,
                s.ClientId,
                s.ProductId,
                s.BillingCycle,
                0,
                s.NextRenewalAt!.Value,
                s.RecurringAmount,
                s.AutoTerminateEndOfCycle,
                _context.CancellationRequests.Any(r => r.ServiceId == s.Id && r.Status == CancellationStatus.Open)))
            .FirstOrDefaultAsync(ct);

        // The paid period is anchored on the day it starts (the end of the trial), not on the day the service
        // was created; read in .NET, as the UTC day, rather than by the database in its session time zone.
        return Result<RenewableServiceDto?>.Success(service is null ? null : service with { AnchorDay = service.NextRenewalAt.Day });
    }
}
