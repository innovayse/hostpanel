namespace Innovayse.Hostpanel.Shared.Middleware;

/// <summary>
/// Strips every response header that is not on the allow-list, just before the response starts.
/// Defence in depth for the white-label rule: no code path copies a provider's headers onto a
/// response, and this makes sure none ever can. <c>LeakGuardTests</c> sends a provider that sets
/// every header it knows and asserts that none arrives.
/// </summary>
public sealed class ProviderLeakGuardMiddleware
{
    /// <summary>The rest of the pipeline.</summary>
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="ProviderLeakGuardMiddleware"/> class.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    public ProviderLeakGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Headers a response may carry. Everything else is removed. <c>Retry-After</c> and
    /// <c>X-RateLimit-Remaining</c> are the edge limiter's 429 contract; they are
    /// ours, never a provider's.
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Length", "Content-Encoding", "Cache-Control", "Date", "Vary", "Location",
        "Retry-After", "X-RateLimit-Remaining", "WWW-Authenticate", "Set-Cookie", "Transfer-Encoding", "Connection",
        "Keep-Alive",
        "X-Request-Id", "X-Accel-Buffering", "Access-Control-Allow-Origin", "Access-Control-Allow-Credentials",
        "Access-Control-Allow-Headers", "Access-Control-Allow-Methods", "Access-Control-Expose-Headers",
        "Access-Control-Max-Age", "Strict-Transport-Security", "X-Content-Type-Options",

        // Set by InlineImage for a plugin icon: our own policy that keeps an SVG opened on its own inert.
        "Content-Security-Policy",

        // Set by the sign-on callback (no-referrer) so the address carrying the code is never sent on as a
        // Referer; the edge keeps it and adds its default only when a response has none.
        "Referrer-Policy",

        // Set only by our own file results (CSV exports, plugin downloads): our value, never a provider's.
        // Without it every download lost its filename once this guard was wired in.
        "Content-Disposition",

        // Set by the static-file middleware for our own uploaded images (branding, slides), which runs
        // behind this guard; stripping them silently disabled caching and range requests.
        "ETag", "Last-Modified", "Accept-Ranges", "Content-Range",

        // Our own sliding-renewal token for a bearer caller (LocalSessionRenewalMiddleware.RenewedTokenHeader);
        // without it the guard removed the renewed credential and the client portal was signed out at expiry.
        "X-Renewed-Access-Token",
    };

    /// <summary>Registers the header sweep and runs the pipeline.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var offending = context.Response.Headers.Keys.Where(key => !Allowed.Contains(key)).ToList();
            foreach (var key in offending)
            {
                context.Response.Headers.Remove(key);
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
