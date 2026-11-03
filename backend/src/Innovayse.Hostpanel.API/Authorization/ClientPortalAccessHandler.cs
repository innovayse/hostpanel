using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Decides <see cref="ClientPortalRequirement"/>. A user holding the client role passes without any plugin
/// call. Anyone else passes only when <see cref="ICallerClientResolver"/> reports an external account, which
/// means an enabled identity plugin vouched for them. Nothing is stored, so a plugin that stops vouching
/// takes effect on the next request; with no plugin a user without the role is still refused. A plugin that
/// throws, times out or is enabled twice vouches for nobody, so the policy refuses and does not throw.
/// </summary>
public sealed class ClientPortalAccessHandler : AuthorizationHandler<ClientPortalRequirement>
{
    /// <summary>Resolves the caller's account through the enabled identity plugin, when there is one.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Initializes the handler.</summary>
    /// <param name="callerClient">Resolves the caller's account through the enabled identity plugin, when there is one.</param>
    public ClientPortalAccessHandler(ICallerClientResolver callerClient)
    {
        _callerClient = callerClient;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ClientPortalRequirement requirement)
    {
        if (context.User.IsInRole(Roles.Client))
        {
            context.Succeed(requirement);
            return;
        }

        // Only a signed-in user can be vouched for; the resolver answers None for anyone else.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var ct = context.Resource is HttpContext http ? http.RequestAborted : CancellationToken.None;
        // The answer is shared with the permission filter and the use case, so a plugin is asked once per request.
        // A plugin that failed to answer (IdentityUnavailable) carries no account, so it is refused here, closed.
        var access = await _callerClient.ResolveAsync(ct);
        if (access.Account is not null)
        {
            context.Succeed(requirement);
        }
    }
}
