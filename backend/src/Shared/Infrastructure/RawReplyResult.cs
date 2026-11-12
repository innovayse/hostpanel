using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Writes bytes to the response as they are, with the status and media type the caller chose: the plain answer a
/// callback sender waits for, such as <c>OK</c> as <c>text/plain</c>, or the HTML page of a gateway's checkout. It sets
/// <c>X-Content-Type-Options: nosniff</c>, so a browser never treats the bytes as another type than the one named.
/// </summary>
/// <remarks>
/// An acknowledgement carries a <c>Content-Security-Policy</c> that allows nothing to load or run and forbids framing, so the
/// bytes are inert even if a browser were to render them. A page (it is given the origins it may reach) carries a strict policy
/// of its own instead: nothing loads or connects except to those origins, the page may not be framed, it has no base address,
/// and it runs sandboxed without the origin of this application, so a script on it cannot read this application's storage or
/// speak with the payer's session. The page is never cached and sends no referrer, because its address can carry a payment token.
/// </remarks>
public sealed class RawReplyResult : IActionResult
{
    /// <summary>The policy sent with every reply that is not a page: nothing may load or run, and the reply may not be framed.</summary>
    private const string InertContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>The HTTP status to answer with.</summary>
    private readonly int _status;

    /// <summary>The bytes to write; may be empty.</summary>
    private readonly ReadOnlyMemory<byte> _body;

    /// <summary>The media type of the bytes.</summary>
    private readonly string _contentType;

    /// <summary>The origins a page may reach, or <see langword="null"/> when the bytes are not a page.</summary>
    private readonly IReadOnlyList<string>? _pageOrigins;

    /// <summary>Initializes the result.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The bytes to write; may be empty.</param>
    /// <param name="contentType">The media type of the bytes.</param>
    /// <param name="pageOrigins">
    /// The exact <c>https</c> origins an HTML page may load from and connect to, none for a page that loads nothing;
    /// <see langword="null"/> when the bytes are not a page and are written inert.
    /// </param>
    public RawReplyResult(int status, ReadOnlyMemory<byte> body, string contentType, IReadOnlyList<string>? pageOrigins = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        _status = status;
        _body = body;
        _contentType = contentType;
        _pageOrigins = pageOrigins;
    }

    /// <summary>Builds the policy of an HTML page that may reach only the given origins.</summary>
    /// <param name="origins">The exact origins the page may load from and connect to; may be empty.</param>
    /// <returns>The <c>Content-Security-Policy</c> header value.</returns>
    public static string PagePolicy(IReadOnlyList<string> origins)
    {
        ArgumentNullException.ThrowIfNull(origins);

        // A directive with no source is invalid, so a page that may reach no origin says 'none' where it would list them.
        var listed = origins.Count == 0 ? "'none'" : string.Join(' ', origins);
        var added = origins.Count == 0 ? string.Empty : $" {string.Join(' ', origins)}";

        // A script and a style of the page itself are the plugin's own markup, so they run inline; what they may load or call is
        // limited to the declared origins, and the sandbox gives the page an opaque origin of its own.
        return $"default-src 'none'; script-src 'unsafe-inline'{added}; style-src 'unsafe-inline'{added}; img-src data:{added}; "
            + $"font-src {listed}; connect-src {listed}; frame-src {listed}; form-action {listed}; base-uri 'none'; "
            + "frame-ancestors 'none'; sandbox allow-scripts allow-forms";
    }

    /// <summary>Writes the status, the headers and the bytes.</summary>
    /// <param name="context">The action context of the request.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
        response.StatusCode = _status;
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = _pageOrigins is null ? InertContentSecurityPolicy : PagePolicy(_pageOrigins);
        if (_pageOrigins is not null)
        {
            response.Headers.CacheControl = "no-store";
            response.Headers["Referrer-Policy"] = "no-referrer";
        }

        // A 204 and a 304 carry no body by definition, so the server would refuse to write one.
        if (_body.IsEmpty || _status is StatusCodes.Status204NoContent or StatusCodes.Status304NotModified)
        {
            return;
        }

        response.ContentType = _contentType;
        response.ContentLength = _body.Length;
        await response.Body.WriteAsync(_body, context.HttpContext.RequestAborted);
    }
}
