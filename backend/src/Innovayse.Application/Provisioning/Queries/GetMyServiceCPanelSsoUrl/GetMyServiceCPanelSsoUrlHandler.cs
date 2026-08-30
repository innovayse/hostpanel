namespace Innovayse.Application.Provisioning.Queries.GetMyServiceCPanelSsoUrl;

using Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;
using Innovayse.Application.Services.Common;
using Wolverine;

/// <summary>
/// Issues a control-panel single-sign-on URL for one of the calling client's own services,
/// refusing every service that is not theirs.
/// </summary>
/// <remarks>
/// <para>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// obtain a panel session through <see cref="GetMyServiceCPanelSsoUrlQuery"/> without it having
/// run. That matters more here than anywhere else on this controller — the value returned is a
/// working login to somebody's files, databases and mail, so the refusal has to precede the
/// provider call rather than sit beside it.
/// </para>
/// <para>
/// Once ownership is settled the work is the same the admin route performs, so this dispatches
/// <see cref="GetCPanelSsoUrlQuery"/> rather than duplicating it. That shared query stays
/// unscoped on purpose: an administrator signing into any client's account is legitimate.
/// </para>
/// </remarks>
/// <param name="ownership">The rule that says a client may only reach their own services.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class GetMyServiceCPanelSsoUrlHandler(IServiceOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyServiceCPanelSsoUrlQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A time-limited control-panel single-sign-on URL.</returns>
    /// <exception cref="MyServiceNotFoundException">
    /// Thrown when the service is not the caller's, when no such service exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<string> HandleAsync(GetMyServiceCPanelSsoUrlQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        return await bus.InvokeAsync<string>(new GetCPanelSsoUrlQuery(query.ServiceId), ct);
    }
}
