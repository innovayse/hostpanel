using System.Threading.RateLimiting;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
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
/// (the use case answers its 404), and is still counted by the global limiter like every request.
/// </para>
/// <para>
/// <b>Who is counted.</b> A client call is counted per client account, resolved by <see cref="ICallerClientResolver"/>, the
/// same answer the portal policy and the use case share (a caller with no single account is counted as that user); an admin
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

    /// <summary>Finds the manifest of the plugin the request names.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Resolves the client account a client call acts for.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>The signed-in caller.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Initializes the filter.</summary>
    /// <param name="area">The panel the controller serves.</param>
    /// <param name="limiter">The limiters of the plugin tiers and streams.</param>
    /// <param name="plugins">Finds the manifest of the plugin the request names.</param>
    /// <param name="callerClient">Resolves the client account a client call acts for.</param>
    /// <param name="currentUser">The signed-in caller.</param>
    public PluginRateLimitFilter(
        PluginArea area,
        PluginRateLimiter limiter,
        IPluginEndpointResolver plugins,
        ICallerClientResolver callerClient,
        ICurrentUserService currentUser)
    {
        _area = area;
        _limiter = limiter;
        _plugins = plugins;
        _callerClient = callerClient;
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
        var manifest = pluginId is null ? null : _plugins.Find(pluginId);
        var routes = manifest is null ? null : _area == PluginArea.Admin ? manifest.Api.Admin : manifest.Api.Client;
        var route = routes is null ? null : PluginRouteMatcher.Match(routes, input.Method, input.Path)?.Route;
        if (manifest is null || route is null)
        {
            await next();
            return;
        }

        var key = PluginRateLimiter.KeyFor(manifest.Id, await SubjectAsync(ct));

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

    /// <summary>Works out who the call is counted for.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>client:{id}</c> for a client call made for one account, <c>user:{id}</c> for a client call that acts for none, and
    /// <c>admin:{id}</c> for an admin call.
    /// </returns>
    private async Task<string> SubjectAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        if (_area == PluginArea.Admin)
        {
            return $"admin:{userId}";
        }

        var access = await _callerClient.ResolveAsync(ct);
        return access.ClientId is { } clientId ? $"client:{clientId}" : $"user:{userId}";
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
