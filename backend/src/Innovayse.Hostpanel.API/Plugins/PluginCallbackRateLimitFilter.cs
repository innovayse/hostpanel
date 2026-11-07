using System.Net;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Measures every request to the public plugin callback door against <see cref="RateLimitPolicies.PluginCallback"/>, counted
/// per caller address, before the request is bound or dispatched.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every request is counted, declared or not.</b> The other plugin filters skip a call the use case would refuse as "no such
/// route", because their callers are signed in and the global limiter still counts the rest. Here the caller is anonymous, so
/// a 429 that spared undeclared routes would tell a scanner which plugin and route are real. Counting everything keeps the
/// answer to a burst the same for an installed plugin and an absent one.
/// </para>
/// <para>
/// <b>The key is the address alone.</b> The plugin id in the URL is the caller's to invent, so a key made from it would let a
/// scanner mint a bucket per invented id. The address is the one resolved through the trusted proxy chain
/// (<c>Connection.RemoteIpAddress</c> after forwarded headers), never a header the caller writes. A refusal is written by
/// <see cref="RateLimitingExtensions.WriteRejectionAsync"/>, so it is the one 429 body with the same headers.
/// </para>
/// </remarks>
public sealed class PluginCallbackRateLimitFilter : IAsyncResourceFilter
{
    /// <summary>The first part of the key; the callback door has no plugin or account to name.</summary>
    private const string KeyScope = "callback";

    /// <summary>The limiters of the plugin tiers.</summary>
    private readonly PluginRateLimiter _limiter;

    /// <summary>Initializes the filter.</summary>
    /// <param name="limiter">The limiters of the plugin tiers.</param>
    public PluginCallbackRateLimitFilter(PluginRateLimiter limiter)
    {
        _limiter = limiter;
    }

    /// <summary>Refuses with 429 when the caller's address has spent the callback budget; otherwise lets the request through.</summary>
    /// <param name="context">The request about to be routed to an action.</param>
    /// <param name="next">Runs the rest of the pipeline.</param>
    /// <returns>A task that completes when the request has been answered.</returns>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var ct = http.RequestAborted;

        // Loopback rather than a shared "unknown" bucket when there is no address, as the global limiter does.
        var address = (http.Connection.RemoteIpAddress ?? IPAddress.Loopback).ToString();
        var key = PluginRateLimiter.KeyFor(KeyScope, $"ip:{address}");

        var lease = await _limiter.AcquireAsync(RateLimitPolicies.PluginCallback, key, ct);
        if (!lease.IsAcquired)
        {
            await RateLimitingExtensions.WriteRejectionAsync(http, RateLimitPolicies.PluginCallback, lease, key, ct);

            // The body is already written; an empty result stops the pipeline without writing a second one.
            context.Result = new EmptyResult();
            return;
        }

        await next();
    }
}
