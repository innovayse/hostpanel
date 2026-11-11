using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetCancellationStatus;

/// <summary>Returns the cancellation status for a given client service.</summary>
public sealed class GetCancellationStatusHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetCancellationStatusHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the query. Checks for an existing open cancellation request on the service.
    /// </summary>
    /// <param name="qry">The service to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: a service with no open request answers "not pending".</returns>
    public async Task<Result<CancellationStatusDto>> HandleAsync(
        GetCancellationStatusQuery qry, CancellationToken ct)
    {
        // Filtered on Open in the query: a service may carry any number of closed requests, and
        // picking an arbitrary one first would hide the open one behind it.
        var openType = await _context.CancellationRequests
            .AsNoTracking()
            .Where(r => r.ServiceId == qry.ServiceId && r.Status == CancellationStatus.Open)
            .Select(r => (CancellationType?)r.Type)
            .FirstOrDefaultAsync(ct);

        return Result<CancellationStatusDto>.Success(new CancellationStatusDto(openType is not null, openType));
    }
}
