using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListUndatedRenewingServices;

/// <summary>Handles <see cref="ListUndatedRenewingServicesQuery"/>: reads the active, auto-renewing services with no renewal date.</summary>
public sealed class ListUndatedRenewingServicesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListUndatedRenewingServicesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Lists the services.</summary>
    /// <param name="query">Marker, no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids, ordered. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(ListUndatedRenewingServicesQuery query, CancellationToken ct)
    {
        _ = query;

        IReadOnlyList<int> undated = await _context.ClientServices.AsNoTracking().NotInTrial()
            .Where(s => s.Status == ServiceStatus.Active && s.AutoRenew && s.NextRenewalAt == null)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(undated);
    }
}
