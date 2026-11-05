using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Writes bytes to the response as they are, with the status and media type the caller chose: the plain answer a
/// callback sender waits for, such as <c>OK</c> as <c>text/plain</c>. It sets <c>X-Content-Type-Options: nosniff</c>, so a
/// browser never treats the bytes as another type than the one named, and a <c>Content-Security-Policy</c> that allows
/// nothing to load or run and forbids framing, so the bytes are inert even if a browser were to render them.
/// </summary>
public sealed class RawReplyResult : IActionResult
{
    /// <summary>The policy sent with every raw reply: nothing may load or run, and the reply may not be framed.</summary>
    private const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>The HTTP status to answer with.</summary>
    private readonly int _status;

    /// <summary>The bytes to write; may be empty.</summary>
    private readonly ReadOnlyMemory<byte> _body;

    /// <summary>The media type of the bytes.</summary>
    private readonly string _contentType;

    /// <summary>Initializes the result.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The bytes to write; may be empty.</param>
    /// <param name="contentType">The media type of the bytes.</param>
    public RawReplyResult(int status, ReadOnlyMemory<byte> body, string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        _status = status;
        _body = body;
        _contentType = contentType;
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
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;

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
