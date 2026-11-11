using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetMyServicePlanChangeOptions;

/// <summary>
/// Lists the plans one of the calling client's own services can move to, with the prorated amount of each move,
/// refusing every service that is not theirs.
/// </summary>
/// <remarks>
/// Ownership is settled first, so a stranger's id never reaches the query that would answer differently for an id that
/// exists. The figures are the ones <c>RequestMyServicePlanChangeHandler</c> charges, because both come from
/// <see cref="ServicePlanChangeQuoter"/>.
/// </remarks>
public sealed class GetMyServicePlanChangeOptionsHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Works out which plans the service can move to and what each move costs.</summary>
    private readonly ServicePlanChangeQuoter _quoter;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="quoter">Works out which plans the service can move to and what each move costs.</param>
    public GetMyServicePlanChangeOptionsHandler(IServiceOwnership ownership, IHostpanelDbContext context, ServicePlanChangeQuoter quoter)
    {
        _ownership = ownership;
        _context = context;
        _quoter = quoter;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="qry">The query. It names no account: this reads the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plans (none when the service has nowhere to move), or <c>SERVICE_NOT_FOUND</c> when the service is not the
    /// caller's, or the quoter's refusal for a service that cannot change plan (<c>SERVICE_NOT_ACTIVE</c>,
    /// <c>SERVICE_CHANGE_NO_RENEWAL_DATE</c>).
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<IReadOnlyList<ServicePlanChangeOptionDto>>> HandleAsync(
        GetMyServicePlanChangeOptionsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(qry.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<IReadOnlyList<ServicePlanChangeOptionDto>>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == qry.ServiceId, ct);
        if (service is null)
        {
            return Result<IReadOnlyList<ServicePlanChangeOptionDto>>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        return await _quoter.OptionsAsync(service, ct);
    }
}
