using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListEndOfCycleTerminationsDue;

/// <summary>Handles <see cref="ListEndOfCycleTerminationsDueQuery"/> with one read.</summary>
public sealed class ListEndOfCycleTerminationsDueHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListEndOfCycleTerminationsDueHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Lists the services.</summary>
    /// <param name="query">The moment the cycle must have ended by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The due services, ordered by id. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<EndOfCycleTerminationDto>>> HandleAsync(
        ListEndOfCycleTerminationsDueQuery query, CancellationToken ct)
    {
        // A terminated service is left out, so a rerun of the caller finds nothing left to do.
        var due = await _context.ClientServices.AsNoTracking().NotInTrial()
            .Where(s => s.AutoTerminateEndOfCycle
                && s.Status != ServiceStatus.Terminated
                && s.NextRenewalAt != null
                && s.NextRenewalAt <= query.DueBy)
            .OrderBy(s => s.Id)
            .Select(s => new EndOfCycleTerminationDto(s.Id, s.AutoTerminateReason))
            .ToListAsync(ct);

        return Result<IReadOnlyList<EndOfCycleTerminationDto>>.Success(due);
    }
}
