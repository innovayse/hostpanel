using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServiceControlPanelSsoUrl;

/// <summary>
/// Issues a control-panel single-sign-on URL for one of the calling client's own services,
/// refusing every service that is not theirs.
/// </summary>
/// <remarks>
/// <para>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// obtain a panel session through <see cref="GetMyServiceControlPanelSsoUrlQuery"/> without it having
/// run. That matters more here than anywhere else on this controller: the value returned is a
/// working login to somebody's files, databases and mail, so the refusal has to precede the
/// provider call rather than sit beside it.
/// </para>
/// <para>
/// Once ownership is settled the work is the same the admin route performs, so this dispatches
/// <see cref="GetControlPanelSsoUrlQuery"/> rather than duplicating it. That shared query stays
/// unscoped on purpose: an administrator signing into any client's account is legitimate.
/// </para>
/// </remarks>
public sealed class GetMyServiceControlPanelSsoUrlHandler
{
    /// <summary>The rule that says a client may only reach their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>The module's data context, for the account the service belongs to.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to read the account's sign-on switch and to reach the shared use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="GetMyServiceControlPanelSsoUrlHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only reach their own services.</param>
    /// <param name="context">The module's data context, for the account the service belongs to.</param>
    /// <param name="bus">Wolverine bus, used to read the account's sign-on switch and to reach the shared use case.</param>
    public GetMyServiceControlPanelSsoUrlHandler(IServiceOwnership ownership, IHostpanelDbContext context, IMessageBus bus)
    {
        _ownership = ownership;
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyServiceControlPanelSsoUrlQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A time-limited control-panel single-sign-on URL, or <c>SERVICE_NOT_FOUND</c> when the
    /// service is not the caller's, when no such service exists, or when the caller has no client
    /// record, <c>CLIENT_SSO_DISABLED</c> (403) when the account does not allow single sign-on,
    /// or <c>SERVICE_NOT_PROVISIONED</c>, <c>SERVICE_NOT_ACTIVE</c> (terminated), <c>SERVICE_NO_SERVER_ASSIGNED</c> or
    /// <c>SERVICE_CPANEL_SSO_UPSTREAM_FAILED</c> from the shared use case.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<ControlPanelSsoDto>> HandleAsync(GetMyServiceControlPanelSsoUrlQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<ControlPanelSsoDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        // The account's switch applies to the client's own sign-in only; the shared query stays
        // unscoped because staff signing in to any account is legitimate.
        var clientId = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Id == query.ServiceId)
            .Select(s => s.ClientId)
            .FirstAsync(ct);
        var mayUseSso = await _bus.EnsureClientMayUsePanelSsoAsync(clientId, ct);
        if (mayUseSso.IsFailure)
        {
            return Result<ControlPanelSsoDto>.FailureCode(mayUseSso.ErrorCode!);
        }

        return await _bus.InvokeAsync<Result<ControlPanelSsoDto>>(new GetControlPanelSsoUrlQuery(query.ServiceId), ct);
    }
}
