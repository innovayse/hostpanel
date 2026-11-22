using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Writes a redirect a plugin's callback asked for: the status, a <c>Location</c> header and no body. The address arrives
/// already checked by the use case, and it is written as it is: a relative address stays relative, so neither the proxy nor
/// the request's own host header is ever trusted to complete it.
/// </summary>
/// <remarks>
/// The reply is never cached and sends no referrer, because the address and the page it leaves can carry a payment token. It
/// sets <c>X-Content-Type-Options: nosniff</c> and the same inert <c>Content-Security-Policy</c> as an acknowledgement
/// (<see cref="RawReplyResult"/>), so a browser that renders the empty answer loads and runs nothing.
/// </remarks>
public sealed class RedirectReplyResult : IActionResult
{
    /// <summary>The HTTP status to answer with: one of 301, 302, 303, 307 or 308.</summary>
    private readonly int _status;

    /// <summary>The address to send the caller to, already checked.</summary>
    private readonly string _location;

    /// <summary>Initializes the result.</summary>
    /// <param name="status">The HTTP status: one of 301, 302, 303, 307 or 308.</param>
    /// <param name="location">The address to send the caller to; the use case has already checked it.</param>
    public RedirectReplyResult(int status, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        _status = status;
        _location = location;
    }

    /// <summary>Writes the status and the headers.</summary>
    /// <param name="context">The action context of the request.</param>
    /// <returns>A completed task: there is no body to write.</returns>
    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
        response.StatusCode = _status;
        response.Headers.Location = _location;
        response.Headers.CacheControl = "no-store";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = RawReplyResult.InertContentSecurityPolicy;
        return Task.CompletedTask;
    }
}
