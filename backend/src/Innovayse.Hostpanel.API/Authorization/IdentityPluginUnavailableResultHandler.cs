using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Answers a refused authorization with 503 <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>) when the
/// refusal is only because the enabled identity plugin could not say who the caller is, and leaves every other
/// refusal to <see cref="ProblemDetailsAuthorizationMiddlewareResultHandler"/>, so a plain 403 stays a 403.
/// </summary>
/// <remarks>
/// The portal policy refuses a roleless plugin member the same way whether the plugin said no or did not answer;
/// only the request memo, filled by the resolver while the policy ran, tells the two apart. Without this the same
/// outage would answer 503 from the permission filter and a bare 403 from the policy.
/// Only a refusal that failed the <see cref="ClientPortalRequirement"/> is eligible, so another policy's 403 stays 403.
/// </remarks>
public sealed class IdentityPluginUnavailableResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>The handler that writes every other 401 and 403.</summary>
    private readonly ProblemDetailsAuthorizationMiddlewareResultHandler _inner = new();

    /// <inheritdoc/>
    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Forbidden && FailedClientPortal(authorizeResult) && IsIdentityUnavailable(context))
        {
            var body = ErrorBody.ForCode(context.RequestServices, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
            await ErrorBody.WriteAsync(context, body, context.RequestAborted);
            return;
        }

        await _inner.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>Tells whether the refusal includes the client portal requirement, the only one the identity plugin decides.</summary>
    /// <param name="authorizeResult">The outcome of the policy evaluation.</param>
    /// <returns><see langword="true"/> when <see cref="ClientPortalRequirement"/> is among the failed requirements.</returns>
    private static bool FailedClientPortal(PolicyAuthorizationResult authorizeResult)
    {
        return authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<ClientPortalRequirement>().Any() == true;
    }

    /// <summary>Tells whether the resolver found the identity plugin unable to answer for this request's caller.</summary>
    /// <param name="context">The current request.</param>
    /// <returns><see langword="true"/> when the kept answer for the signed-in subject is the unavailable one.</returns>
    private static bool IsIdentityUnavailable(HttpContext context)
    {
        var services = context.RequestServices;
        var userId = services.GetRequiredService<ICurrentUserService>().UserId;

        return userId is not null
            && services.GetRequiredService<ICallerClientMemo>().TryGet(userId, out var kept)
            && kept.IdentityUnavailable;
    }
}
