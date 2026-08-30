namespace Innovayse.API.Auth;

using System.Net;
using System.Text.Json;

/// <summary>
/// Refuses a state-changing request that is authenticated by the local session cookie alone and
/// does not carry the header a cross-site page cannot set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is needed now and was not before.</b> A bearer token in an <c>Authorization</c>
/// header is inherently CSRF-safe: a cross-site page cannot attach it. Local mode relied on
/// that, which is why its branch of the pipeline has no CSRF check at all — the SSO branch gets
/// one from <c>UseInnovayseAuth</c>, and it is there because that branch has cookies.
/// Moving the local session onto a cookie moves local mode into the same risk, so it gets the
/// same defence.
/// </para>
/// <para>
/// <b>Two defences, not one.</b> The cookie is <c>SameSite=Strict</c>, so a compliant browser
/// will not attach it to a cross-site request in the first place. This is the second layer, for
/// the request shapes and the clients where that guarantee is weaker than it reads.
/// </para>
/// <para>
/// <b>What it deliberately does not refuse.</b> Only requests that actually present the session
/// cookie are examined. A caller with an <c>Authorization</c> header is left alone — the client
/// portal's Nuxt server is one, and it has no browser and sets no such header. So is every
/// anonymous POST: payment-gateway webhooks arrive with no cookie and no custom headers, and a
/// blanket header requirement would silently stop settling invoices.
/// </para>
/// <para>
/// Registered only in <c>Program.cs</c>'s local branch. The SSO path does not run it and is
/// unchanged.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class LocalSessionCsrfMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Header a cross-site page cannot set without a CORS preflight this API does not grant.
    /// The admin SPA's fetch wrapper sends it on every request.
    /// </summary>
    private const string RequestedWithHeader = "X-Requested-With";

    /// <summary>Value the admin SPA sends in <see cref="RequestedWithHeader"/>.</summary>
    private const string RequestedWithValue = "XMLHttpRequest";

    /// <summary>Machine-readable code sent to a refused caller.</summary>
    private const string Code = "CSRF_HEADER_MISSING";

    /// <summary>
    /// Methods that cannot change state, and are therefore never refused.
    /// </summary>
    private static readonly HashSet<string> _safeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the request has been handled or refused.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresHeader(context.Request) && !HasHeader(context.Request))
        {
            // Written here rather than thrown, because ExceptionMiddleware sits above this in
            // the pipeline and a refusal that has to travel back up through it just to be
            // written out is indirection with no reader. The body keeps the { error, code }
            // shape every other refusal on this API uses.
            //
            // The sentence is English and is not resourced, and that is deliberate rather than
            // an oversight: nothing renders it. A browser that reaches this either is not the
            // admin panel or has been made to send a request the admin panel never sends, and
            // in both cases the only reader is somebody with the response in front of them.
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = $"A state-changing request authenticated by session cookie must send the {RequestedWithHeader} header.",
                code = Code,
            }));
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Whether this request is one the header is required on.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>True when it changes state and is relying on the session cookie.</returns>
    private static bool RequiresHeader(HttpRequest request) =>
        !_safeMethods.Contains(request.Method)
        && request.Cookies.ContainsKey(LocalSessionCookie.Name)
        && !request.Headers.ContainsKey("Authorization");

    /// <summary>
    /// Whether the request carries the expected header.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>True when the header is present with the expected value.</returns>
    private static bool HasHeader(HttpRequest request) =>
        request.Headers.TryGetValue(RequestedWithHeader, out var values)
        && values.Any(v => string.Equals(v, RequestedWithValue, StringComparison.OrdinalIgnoreCase));
}
