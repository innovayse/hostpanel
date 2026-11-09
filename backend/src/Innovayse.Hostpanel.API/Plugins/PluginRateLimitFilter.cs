using System.Threading.RateLimiting;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Measures a plugin route against the budget of the tier its manifest declares, before the request is bound or
/// dispatched: <see cref="RateLimitPolicies.PluginDefault"/> for a route that names none,
/// <see cref="RateLimitPolicies.PluginStrict"/> or <see cref="RateLimitPolicies.PluginHeavy"/> for one that asks for them,
/// counted per plugin and per caller, and, for a streaming route, the number of streams the caller already holds open.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a filter.</b> The tier belongs to the matched route and the key to the plugin and the account, and neither is
/// known to the rate limiting middleware, which runs before routing reaches the plugin and picks its policy from an
/// attribute. This filter is the first point where the signed-in caller, the plugin and the route are all known, and it
/// is still ahead of model binding, the body and the bus. A request that matches no declared route is not measured here
/// (the use case answers its 404), and neither is a call that use case refuses as "no such route": a disabled or
/// unconfigured plugin, a route the caller lacks the permission for, a caller with no account. The decision is
/// <see cref="PluginCallGate"/>'s, the one the use cases ask, so a 429 never tells an installed plugin, or a route that is
/// not the caller's to see, from one that is not there. All are still counted by the global limiter like every request.
/// </para>
/// <para>
/// <b>Who is counted.</b> A client call is counted per client account, resolved through <see cref="PluginCallGate"/>, the
/// same answer the portal policy and the use case share (a caller with no single account is not counted); an admin
/// call is counted per administrator. The plugin id is part of the key, so a busy plugin never spends another plugin's
/// budget.
/// </para>
/// <para>
/// <b>Streams.</b> The stream lease is registered for disposal with the response, so it is held until the response has been
/// written and the connection is done with it, not merely until this filter returns.
/// </para>
/// <para>
/// A refusal is written by <see cref="RateLimitingExtensions.WriteRejectionAsync"/>, the writer the middleware uses, so it
/// is the one 429 body with the same headers.
/// </para>
/// </remarks>
public sealed class PluginRateLimitFilter : IAsyncResourceFilter
{
    /// <summary>The panel the controller serves; only the routes of this area are looked at.</summary>
    private readonly PluginArea _area;

    /// <summary>The limiters of the plugin tiers and streams.</summary>
    private readonly PluginRateLimiter _limiter;

    /// <summary>Decides whether the use case would serve the call or refuse it as "no such route"; the same gate the use cases ask.</summary>
    private readonly PluginCallGate _gate;

    /// <summary>The signed-in caller.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Initializes the filter.</summary>
    /// <param name="area">The panel the controller serves.</param>
    /// <param name="limiter">The limiters of the plugin tiers and streams.</param>
    /// <param name="gate">Decides whether the use case would serve the call or refuse it as "no such route".</param>
    /// <param name="currentUser">The signed-in caller.</param>
    public PluginRateLimitFilter(
        PluginArea area,
        PluginRateLimiter limiter,
        PluginCallGate gate,
        ICurrentUserService currentUser)
    {
        _area = area;
        _limiter = limiter;
        _gate = gate;
        _currentUser = currentUser;
    }

    /// <summary>Refuses with 429 when the route's tier, or the caller's open streams, are spent; otherwise lets the request through.</summary>
    /// <param name="context">The request about to be routed to an action.</param>
    /// <param name="next">Runs the rest of the pipeline.</param>
    /// <returns>A task that completes when the request has been answered.</returns>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var ct = http.RequestAborted;

        // The same route values the action binds, and the same reader, so this filter and the use case agree on the method
        // and the path they match.
        var pluginId = context.RouteData.Values["id"] as string;
        var input = PluginHttpInputReader.Read(http.Request, context.RouteData.Values["path"] as string);
        if (pluginId is null)
        {
            await next();
            return;
        }

        // The gate is the use case's own decision: a call it would refuse as "no such route" (unknown or disabled plugin,
        // undeclared route, wrong method, missing permission, no account) is not measured, or a 429 after a burst would tell
        // an installed plugin, or a route the caller may not see, from one that is not there.
        var decision = _area == PluginArea.Admin
            ? await _gate.EvaluateAdminAsync(pluginId, input, ct)
            : await _gate.EvaluateClientAsync(pluginId, input, ct);
        if (decision is not { Target: { } target })
        {
            await next();
            return;
        }

        var manifest = target.Manifest;
        var route = target.Match.Route;
        var subject = _area == PluginArea.Admin
            ? $"admin:{_currentUser.RequireUserId()}"
            : $"client:{decision.ClientId}";

        var key = PluginRateLimiter.KeyFor(manifest.Id, subject);

        var tier = TierOf(route);
        var lease = await _limiter.AcquireAsync(tier, key, ct);
        if (!lease.IsAcquired)
        {
            await RefuseAsync(context, tier, lease, key, ct);
            return;
        }

        if (route.Streaming)
        {
            var stream = await _limiter.AcquireStreamAsync(key, ct);
            if (!stream.IsAcquired)
            {
                await RefuseAsync(context, RateLimitPolicies.PluginStream, stream, key, ct);
                return;
            }

            http.Response.RegisterForDispose(stream);
        }

        await next();
    }

    /// <summary>Names the tier a route is measured against.</summary>
    /// <param name="route">The matched route.</param>
    /// <returns>The policy name: the one the route declares, or <see cref="RateLimitPolicies.PluginDefault"/> when it declares none.</returns>
    private static string TierOf(PluginRoute route)
    {
        return route.RateLimit switch
        {
            PluginRateLimitPolicy.Strict => RateLimitPolicies.PluginStrict,
            PluginRateLimitPolicy.Heavy => RateLimitPolicies.PluginHeavy,
            _ => RateLimitPolicies.PluginDefault,
        };
    }

    /// <summary>Answers the request with the platform's 429.</summary>
    /// <param name="context">The request being refused.</param>
    /// <param name="policy">The policy that refused it.</param>
    /// <param name="lease">The lease that was refused.</param>
    /// <param name="key">The bucket that was spent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the response has been written.</returns>
    private static async Task RefuseAsync(
        ResourceExecutingContext context, string policy, RateLimitLease lease, string key, CancellationToken ct)
    {
        await RateLimitingExtensions.WriteRejectionAsync(context.HttpContext, policy, lease, key, ct);

        // The body is already written; an empty result stops the pipeline without writing a second one.
        context.Result = new EmptyResult();
    }
}
