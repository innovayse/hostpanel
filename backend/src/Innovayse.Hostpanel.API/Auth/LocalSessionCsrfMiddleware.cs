using Innovayse.Hostpanel.Shared.Infrastructure;

namespace Innovayse.Hostpanel.API.Auth;

/// <summary>
/// Refuses a state-changing request that is authenticated by the local session cookie alone and
/// does not carry the header a cross-site page cannot set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is needed.</b> A bearer token in an <c>Authorization</c> header cannot be attached
/// by a cross-site page; a session cookie can. The local session is a cookie, so it gets the
/// same CSRF defence the external sign-on branch gets from its own authentication package.
/// </para>
/// <para>
/// <b>Two defences, not one.</b> The cookie is <c>SameSite=Strict</c>, so a compliant browser
/// will not attach it to a cross-site request in the first place. This is the second layer, for
/// the request shapes and the clients where that guarantee is weaker than it reads.
/// </para>
/// <para>
/// <b>What it deliberately does not refuse.</b> Only requests that actually present the session
/// cookie are examined. A caller with an <c>Authorization</c> header is left alone: the client
/// portal's Nuxt server is one, and it has no browser and sets no such header. So is every
/// anonymous POST: payment-gateway webhooks arrive with no cookie and no custom headers, and a
/// blanket header requirement would silently stop settling invoices.
/// </para>
/// <para>
/// The rule itself (which methods change state, what counts as a bearer, which header) is <see cref="CsrfRule"/>,
/// shared with <see cref="RequireRequestedWithAttribute"/>. Registered only in <c>Program.cs</c>'s local branch.
/// The external sign-on branch does not run it.
/// </para>
/// </remarks>
public sealed class LocalSessionCsrfMiddleware
{
    /// <summary>The next middleware in the pipeline.</summary>
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of the <see cref="LocalSessionCsrfMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public LocalSessionCsrfMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the request has been handled or refused.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresHeader(context.Request) && !CsrfRule.HasRequestedWith(context.Request))
        {
            // Written here rather than thrown, because ExceptionHandlingMiddleware sits above
            // this in the pipeline and a refusal that has to travel back up through it just to be
            // written out is indirection with no reader. The body is the one ErrorResponse, with
            // the sentence in the caller's X-Language like every other refusal.
            await ErrorBody.WriteAsync(context, CsrfRule.Refusal(context.RequestServices), context.RequestAborted);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Whether this request is one the header is required on.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>True when it changes state and is relying on the session cookie.</returns>
    /// <remarks>
    /// Only a real bearer token exempts a request: any other <c>Authorization</c> value (a Basic
    /// pair, an empty or malformed scheme) authenticates nothing here, so the cookie is still what
    /// the request rides on and it still needs the header.
    /// </remarks>
    private static bool RequiresHeader(HttpRequest request) =>
        CsrfRule.ChangesState(request)
        && request.Cookies.ContainsKey(LocalSessionCookie.Name)
        && !CsrfRule.HasBearer(request);
}
