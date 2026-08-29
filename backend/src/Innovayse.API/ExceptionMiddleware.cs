namespace Innovayse.API;

using System.Net;
using System.Text.Json;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Domains.Common;
using Innovayse.Application.Resources;
using Innovayse.Application.Support.Common;
using Microsoft.Extensions.Localization;

/// <summary>
/// Global exception-handling middleware that maps well-known exceptions to HTTP status codes.
/// Prevents unhandled exceptions from propagating to the test host or client.
/// </summary>
/// <remarks>
/// <para>
/// This is also the single place a refusal acquires its <b>wording</b>. The response body keeps
/// the shape it always had -- <c>{ "error": "<i>sentence</i>", "code": "<i>CODE</i>" }</c> -- but
/// the sentence for every exception type named below is now resolved from
/// <c>Innovayse.Application/Resources/ValidationMessages*.resx</c> in the culture
/// <c>UseRequestLocalization</c> read off <c>Accept-Language</c>. The portal ships en/ru/hy and
/// the frontend no longer keeps a mapping table of its own, so a Russian or Armenian customer
/// reads the refusal in their own language rather than the five the portal happened to have
/// entries for.
/// </para>
/// <para>
/// <b>Only typed exceptions are looked up.</b> A plain <see cref="InvalidOperationException"/> or
/// <see cref="UnauthorizedAccessException"/> still travels with its own
/// <see cref="Exception.Message"/>: the handler that threw it is the only thing that knows which
/// resource key applies, so a handler whose refusal a person reads resolves the sentence itself
/// through <c>IStringLocalizer&lt;ValidationMessages&gt;</c> before throwing. Localising here by
/// guessing at the message text would be exactly the string-matching this contract exists to
/// avoid.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Logger for unhandled exceptions.</param>
/// <param name="localizer">
/// The refusal sentences, in the culture of the request being answered. Injected into the
/// middleware's constructor rather than resolved per request on purpose: the localizer reads
/// <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> at the moment of the lookup,
/// not at construction, so one instance answers every culture correctly.
/// </param>
public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IStringLocalizer<ValidationMessages> localizer)
{
    /// <summary>
    /// Resource key for <see cref="InternalErrorCode"/>'s sentence. The other keys are constants
    /// on the exception types themselves, beside the codes they travel with; this one has no
    /// exception type to hang off because it is what the catch-all writes.
    /// </summary>
    private const string InternalErrorKey = "InternalError";

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

    /// <summary>
    /// Code for a request that failed the FluentValidation middleware Wolverine runs in front of
    /// every handler that has a validator.
    /// </summary>
    private const string ValidationFailedCode = "VALIDATION_FAILED";

    /// <summary>
    /// Resource key for <see cref="ValidationFailedCode"/>'s sentence. Like
    /// <see cref="InternalErrorKey"/> it is a constant here rather than on an exception type,
    /// because the exception is FluentValidation's and carries nothing of this project's.
    /// </summary>
    private const string ValidationFailedKey = "ValidationFailed";

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
                context, HttpStatusCode.NotFound, Localize(ClientProfileNotFoundException.MessageKey),
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
                context, HttpStatusCode.NotFound, Localize(TicketNotFoundException.MessageKey),
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
                context, HttpStatusCode.NotFound, Localize(InvoiceNotFoundException.MessageKey),
                InvoiceNotFoundException.Code);
        }
        catch (DomainNotFoundException ex)
        {
            // The same answer as the two refusals above, for the same reason. These routes used
            // to answer 403 for all three cases, which withheld as much but disagreed with the
            // ticket and invoice routes for no recoverable reason; they now refuse alike, and
            // 404 is the stricter of the two because a 403 asserts the resource exists.
            logger.LogInformation(
                "Domain {DomainId} refused to its requester; answering {Code}.", ex.DomainId, DomainNotFoundException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.NotFound, Localize(DomainNotFoundException.MessageKey),
                DomainNotFoundException.Code);
        }
        catch (ContactRecipientNotConfiguredException)
        {
            // 503, not 400 and not 500: the submission was well-formed and nothing failed at
            // runtime -- this deployment has never been able to answer it. The visitor is told to
            // use another channel rather than to retry, because retrying cannot work until an
            // operator sets the value.
            //
            // The setting is named in this log line and nowhere in the response. The handler logs
            // its own line at Error too; this one records that the refusal reached the wire.
            logger.LogWarning(
                "Contact form refused: {Setting} is unset; answering {Code}.",
                ContactRecipientNotConfiguredException.SettingName, ContactRecipientNotConfiguredException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.ServiceUnavailable, Localize(ContactRecipientNotConfiguredException.MessageKey),
                ContactRecipientNotConfiguredException.Code);
        }
        catch (ContactMessageNotSentException ex)
        {
            // 502: this service was fine and the SMTP relay behind it was not. The relay's own
            // message stays in this log line -- it names hosts, and sometimes credentials -- while
            // the response says only that the message did not arrive and a retry is worth making.
            logger.LogError(
                ex.InnerException ?? ex, "Contact form message was not delivered; answering {Code}.",
                ContactMessageNotSentException.Code);

            await WriteErrorAsync(
                context, HttpStatusCode.BadGateway, Localize(ContactMessageNotSentException.MessageKey),
                ContactMessageNotSentException.Code);
        }
        catch (FluentValidation.ValidationException ex)
        {
            // 400: the message never reached its handler because it did not satisfy the validator
            // Wolverine runs in front of it. Answered here rather than by an IExceptionHandler of
            // its own so that a validation refusal has the same body as every other refusal --
            // { error, code } -- and the client BFF's internalApiCall keeps reading one shape.
            //
            // The per-field failures are logged and not sent. The response contract on this API is
            // one localised sentence plus one machine-readable code, and the sentences live in
            // ValidationMessages*.resx while a validator's WithMessage text is an English literal in the
            // Application assembly; putting that literal in `error` would answer a Russian or
            // Armenian caller in English, which is the thing this contract was rewritten to stop.
            // Information, not Error or Warning: a rejected form is ordinary traffic, and a stack
            // trace per bad submission is the log noise ControlFlowExceptionLogFilter exists over.
            logger.LogInformation(
                "Validation refused a message; answering {Code}. Failures: {Failures}",
                ValidationFailedCode,
                string.Join("; ", ex.Errors.Select(failure => $"{failure.PropertyName}: {failure.ErrorMessage}")));

            await WriteErrorAsync(
                context, HttpStatusCode.BadRequest, Localize(ValidationFailedKey), ValidationFailedCode);
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
                context, HttpStatusCode.InternalServerError, Localize(InternalErrorKey), InternalErrorCode);
        }
    }

    /// <summary>
    /// Resolves one refusal sentence in the culture of the request currently being answered.
    /// </summary>
    /// <param name="key">Key in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.</param>
    /// <returns>The sentence, or the key itself when no resource carries it.</returns>
    /// <remarks>
    /// A missing key returns the key text rather than throwing, which is
    /// <see cref="IStringLocalizer"/>'s own behaviour and is kept on purpose: a refusal must still
    /// reach the caller with its status and its code even when somebody forgot the resource entry,
    /// and <c>ClientProfileNotFound</c> on a screen is a visible defect where an exception inside
    /// the exception handler is an opaque 500.
    /// </remarks>
    private string Localize(string key) => localizer[key];

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
