namespace Innovayse.API;

using System.Net;
using System.Text.Json;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Support.Common;

/// <summary>
/// Global exception-handling middleware that maps well-known exceptions to HTTP status codes.
/// Prevents unhandled exceptions from propagating to the test host or client.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Logger for unhandled exceptions.</param>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    /// <summary>Code for a request that carried no usable credentials.</summary>
    private const string UnauthorizedCode = "UNAUTHORIZED";

    /// <summary>
    /// Code for the unclassified 400s -- a handler that refused with a plain
    /// <see cref="InvalidOperationException"/> and no code of its own. A caller that sees this
    /// has only the sentence to go on; give a refusal its own exception type when the frontend
    /// needs to act on it.
    /// </summary>
    private const string InvalidOperationCode = "INVALID_OPERATION";

    /// <summary>Code for anything that reached the last handler unclassified.</summary>
    private const string InternalErrorCode = "INTERNAL_ERROR";

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ClientProfileNotFoundException ex)
        {
            // Answered 404, not 400: the caller is authenticated and the request was
            // well-formed -- the "my client profile" resource genuinely does not exist for
            // this identity. 404 is also what the client BFF already probes for when it
            // decides a staff identity gets identity fields with empty billing defaults.
            //
            // The user id is written here and nowhere else. Information, not Error: a staff
            // account browsing the client portal is an expected shape of traffic, and logging
            // it at error level would make a clean system look like it is failing.
            logger.LogInformation(
                "No client profile for user {UserId}; answering {Code}.", ex.UserId, ClientProfileNotFoundException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.NotFound, ClientProfileNotFoundException.PublicMessage,
                ClientProfileNotFoundException.Code);
        }
        catch (TicketNotFoundException ex)
        {
            // Answered 404, and answered the same whether the ticket is somebody else's, does not
            // exist, or the caller has no client record at all. A 403 for one and a 404 for
            // another would tell a caller walking /api/me/tickets/1,2,3 exactly which ids are
            // real -- the enumeration this refusal exists to prevent.
            //
            // The ticket id is written here and nowhere else. Information, not Error: a stale
            // bookmark produces this too, and a probe is not a fault in this service.
            logger.LogInformation(
                "Ticket {TicketId} refused to its requester; answering {Code}.", ex.TicketId, TicketNotFoundException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.NotFound, TicketNotFoundException.PublicMessage,
                TicketNotFoundException.Code);
        }
        catch (InvoiceNotFoundException ex)
        {
            // The same answer as the ticket refusal above, for the same reason: an invoice that
            // is somebody else's, one that does not exist, and a caller with no client record
            // must not be distinguishable, or /api/me/invoices/{id} becomes an id oracle.
            logger.LogInformation(
                "Invoice {InvoiceId} refused to its requester; answering {Code}.", ex.InvoiceId, InvoiceNotFoundException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.NotFound, InvoiceNotFoundException.PublicMessage,
                InvoiceNotFoundException.Code);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, ex.Message, UnauthorizedCode);
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message, InvalidOperationCode);
        }
        catch (Exception ex)
        {
            // The response body deliberately never includes ex.Message — this is the last
            // handler in the pipeline, so whatever reaches here hasn't been classified as a
            // client-facing error by anything upstream, and echoing it back risks leaking
            // internals (connection strings, stack frames) to the caller. This log line is
            // the only place the real exception surfaces; previously it was discarded
            // entirely (`_ = ex;`), which is how a JwtTokenService config bug went unnoticed
            // through every 500 it caused.
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(
                context, HttpStatusCode.InternalServerError, "An unexpected error occurred.", InternalErrorCode);
        }
    }

    /// <summary>
    /// Writes a JSON error response with the specified status code, message and code.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="message">The human-readable error message, shown to the person.</param>
    /// <param name="code">The machine-readable code the caller branches on.</param>
    private static async Task WriteErrorAsync(
        HttpContext context, HttpStatusCode statusCode, string message, string code)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // `error` keeps its original meaning -- the sentence -- because every existing caller
        // (the client BFF's internalApiCall among them) already reads it as one. `code` is
        // added beside it so a caller can tell two failures apart without matching on English
        // prose. A fetch caller gets a code and a meaningful status; the sentence is for the
        // person reading the screen, not for the code deciding what to render.
        var body = JsonSerializer.Serialize(new { error = message, code });
        await context.Response.WriteAsync(body);
    }
}
