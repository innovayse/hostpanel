using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelLoginDetails;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServicePanelLoginDetails;

/// <summary>
/// Reads how the calling client signs in by hand to the panel of one of their own services, refusing every service that is not
/// theirs. It is the by-hand twin of the single sign on route: the same ownership rule and the same account switch apply (a client
/// whose account does not allow single sign on is not shown a way into the panel here either), and once they are settled it
/// dispatches <see cref="GetPanelLoginDetailsQuery"/> rather than repeating it.
/// </summary>
public sealed class GetMyServicePanelLoginDetailsHandler
{
    /// <summary>The rule that says a client may only reach their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>The module's data context, for the account the service belongs to.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to read the account's sign on switch and to reach the shared use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="GetMyServicePanelLoginDetailsHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only reach their own services.</param>
    /// <param name="context">The module's data context, for the account the service belongs to.</param>
    /// <param name="bus">Wolverine bus, used to read the account's sign on switch and to reach the shared use case.</param>
    public GetMyServicePanelLoginDetailsHandler(IServiceOwnership ownership, IHostpanelDbContext context, IMessageBus bus)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyServicePanelLoginDetailsQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The login details without a password, or <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, when no such service
    /// exists, or when the caller has no client record, <c>CLIENT_SSO_DISABLED</c> (403) when the account does not allow single sign on,
    /// or the refusal of the shared read.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<PanelLoginDetailsDto>> HandleAsync(GetMyServicePanelLoginDetailsQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<PanelLoginDetailsDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var clientId = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Id == query.ServiceId)
            .Select(s => s.ClientId)
            .FirstAsync(ct);
        var mayUseSso = await _bus.EnsureClientMayUsePanelSsoAsync(clientId, ct);
        if (mayUseSso.IsFailure)
        {
            return Result<PanelLoginDetailsDto>.FailureCode(mayUseSso.ErrorCode!);
        }

        return await _bus.InvokeAsync<Result<PanelLoginDetailsDto>>(new GetPanelLoginDetailsQuery(query.ServiceId), ct);
    }
}
