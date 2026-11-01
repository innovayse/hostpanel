using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Shared.Controllers;

/// <summary>
/// The base of every controller: the members that turn a <see cref="Result"/> into HTTP. A
/// controller binds, dispatches once and hands the outcome here; it never names a status code
/// and never builds a body. Authorization is required by default so that a controller added
/// without an attribute is closed rather than open. The one status declared here is the one every
/// action can answer: 500 from the exception middleware. Every other status is declared where it is
/// true: 400 and 422 on the actions that bind input or map a refusal to them, 401 on the actions
/// behind authentication, and 429 on each controller the rate limiter measures (every one except the
/// exempt liveness probe).
/// </summary>
[Authorize]
[ApiController]
[ServiceFilter(typeof(ActionTimingFilter))]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>Answers a refusal in the product's shape.</summary>
    /// <param name="type">The error code.</param>
    /// <param name="title">The class of failure.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="detail">The localized sentence.</param>
    /// <param name="errors">Field-level messages, if any.</param>
    /// <returns>The response.</returns>
    protected IActionResult Error(
        string type, string title, int status, string detail, Dictionary<string, List<string>>? errors = null)
    {
        return ErrorBody.ToActionResult(new ErrorResponse(type, title, status, detail, errors));
    }

    /// <summary>200 with the value, or the mapped refusal.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure ? MapFailure(result) : Ok(result.Value);
    }

    /// <summary>204, or the mapped refusal.</summary>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure ? MapFailure(result) : NoContent();
    }

    /// <summary>200 with an empty body, or the mapped refusal.</summary>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleResultOk(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure ? MapFailure(result) : Ok();
    }

    /// <summary>200 with data that did not come from a use case (a health probe, a static list).</summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="data">The payload written as the response body.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleData<T>(T data)
    {
        return Ok(data);
    }

    /// <summary>201 with a relative location.</summary>
    /// <typeparam name="T">The created resource type.</typeparam>
    /// <param name="relativeUri">Where the resource now lives, relative to the API root.</param>
    /// <param name="value">The resource.</param>
    /// <returns>The response.</returns>
    protected IActionResult CreatedResult<T>(string relativeUri, T value)
    {
        return Created(relativeUri, value);
    }

    /// <summary>
    /// A sign-in answer: sets the httpOnly session cookie when the outcome carries a session token,
    /// and answers 200 with the outcome itself as the body, or answers the mapped refusal.
    /// </summary>
    /// <remarks>
    /// The token stays in the body as well as in the cookie, because the sign-in endpoints have a
    /// second class of caller, anything scripted or machine-to-machine, which has no cookie jar.
    /// An outcome with no session token (a second factor is still owed) sets no cookie.
    /// </remarks>
    /// <typeparam name="T">The sign-in outcome type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleSession<T>(Result<T> result)
        where T : ISessionGrant
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure || result.Value is null)
        {
            return MapFailure(result);
        }

        if (result.Value.SessionToken is { } token)
        {
            LocalSessionCookie.Issue(HttpContext, token);
        }

        return Ok(result.Value);
    }

    /// <summary>Clears the httpOnly session cookie and answers 200 with the value, or answers the mapped refusal.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleSessionEnded<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return MapFailure(result);
        }

        LocalSessionCookie.Clear(HttpContext);
        return Ok(result.Value);
    }

    /// <summary>A success that is not a plain 200; the refusal path stays identical.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <param name="onSuccess">What to answer with on success.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value!) : MapFailure(result);
    }

    /// <summary>The file download the handler produced, or the mapped refusal.</summary>
    /// <remarks>
    /// Every download is marked <c>private, no-store</c>. What goes out this way is an invoice or
    /// a report about somebody's account, so neither the browser nor a proxy in between should keep
    /// a copy for the next person at the same machine or behind the same cache.
    /// </remarks>
    /// <typeparam name="T">The export DTO type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>A file response with the DTO's bytes, content type and download name.</returns>
    protected IActionResult FileResult<T>(Result<T> result)
        where T : IFileDownload
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure || result.Value is null)
        {
            return MapFailure(result);
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.FileContent, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>202 with no body for queued work, or the mapped refusal.</summary>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult AcceptedResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure ? MapFailure(result) : StatusCode(StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// The reply a use case forwarded from a plugin, or the mapped refusal: a success answers with the status the
    /// plugin chose and its JSON body (or none), and a problem answers in the standard error body with that status.
    /// </summary>
    /// <remarks>
    /// The one place a status that is not ours to choose reaches the wire, so no controller names one. A problem
    /// carries its finished sentence already, in the caller's language, and a 503 problem also carries
    /// <c>Retry-After</c> like every other 503.
    /// </remarks>
    /// <typeparam name="T">The forwarded reply type.</typeparam>
    /// <param name="result">The handler's outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult HandleForwarded<T>(Result<T> result)
        where T : IForwardedReply
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure || result.Value is null)
        {
            return MapFailure(result);
        }

        var reply = result.Value;
        if (reply.ErrorCode is not null)
        {
            RetryAfterHeader.ApplyFor(HttpContext, reply.Status);
            return Error(reply.ErrorCode, ReasonPhrases.GetReasonPhrase(reply.Status), reply.Status, reply.Detail ?? string.Empty);
        }

        // A JsonResult, not an ObjectResult: content negotiation would write a plugin's bare string as text/plain, and a
        // plugin that returned a JSON document must get a JSON document whatever the caller's Accept header says.
        return reply.Body is null ? StatusCode(reply.Status) : new JsonResult(reply.Body) { StatusCode = reply.Status };
    }

    /// <summary>Maps a refusal to its status and localizes its sentence by code; a 503 also carries <c>Retry-After</c>.</summary>
    /// <remarks>
    /// Protected so that an action needing a non-default success shape routes its failure through
    /// the one canonical mapping instead of choosing a status itself.
    /// </remarks>
    /// <param name="result">A failed outcome.</param>
    /// <returns>The response.</returns>
    protected IActionResult MapFailure(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var code = result.ErrorCode ?? SharedErrorCodes.Unexpected;
        var (status, title) = StatusMapping.For(code);

        // Localized by code, not by the handler's English sentence: the resource file holds the
        // person-facing wording in every language, and the handler's Error string goes to the log.
        var resolver = HttpContext.RequestServices.GetRequiredService<IErrorMessageResolver>();
        RetryAfterHeader.ApplyFor(HttpContext, status);
        return Error(code, title, status, resolver.Resolve(code), result.Errors);
    }
}
