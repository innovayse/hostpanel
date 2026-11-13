using System.Data.Common;
using System.Text.Json;
using FluentValidation;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Resources;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Shared.Middleware;

/// <summary>
///     Global exception handling middleware.
///     Converts exceptions to appropriate HTTP responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    /// <summary>String localizer for error titles and details.</summary>
    private readonly IStringLocalizer<ValidationMessages> _localizer;

    /// <summary>Logger instance.</summary>
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Next request delegate in pipeline.</summary>
    private readonly RequestDelegate _next;

    /// <summary>
    ///     Initializes exception handling middleware
    /// </summary>
    /// <param name="next">Next request delegate in pipeline</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="localizer">String localizer for error messages</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IStringLocalizer<ValidationMessages> localizer)
    {
        _next = next;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    ///     Invokes middleware to handle request and catch exceptions
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="errorMessageResolver">
    ///     Resolves a <see cref="DomainException"/>'s code to its localized sentence. Taken as a
    ///     method parameter, not a constructor one: it is registered scoped, and this middleware is
    ///     constructed once for the app's lifetime, so a constructor parameter would capture a
    ///     scoped instance for the whole process. ASP.NET Core resolves an <c>InvokeAsync</c>
    ///     parameter from the current request's own service provider instead.
    /// </param>
    /// <returns>A task that completes when the pipeline (or the error response) has run.</returns>
    public async Task InvokeAsync(HttpContext context, IErrorMessageResolver errorMessageResolver)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected or its request timed out: this is not an application
            // error (the connection is gone, so there is no one to write a response to), just
            // routine network behavior. Do NOT map it to 500: that would mislabel expected
            // client-side cancellation as an "unhandled exception" in logs/observability and
            // could throw again trying to write to an aborted connection.
            _logger.LogDebug(
                "Request canceled by client for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, errorMessageResolver);
        }
    }

    /// <summary>
    ///     Handles exception and converts to appropriate HTTP response
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="exception">Exception to handle</param>
    /// <param name="errorMessageResolver">Resolves a <see cref="DomainException"/>'s code to its localized sentence.</param>
    /// <returns>A task that completes when the response is written.</returns>
    private async Task HandleExceptionAsync(
        HttpContext context, Exception exception, IErrorMessageResolver errorMessageResolver)
    {
        var response = exception switch
        {
            // An aggregate's own invariant, thrown as the last line of defence.
            // Mapped the same way a handler's own Result refusal is mapped in
            // BaseApiController.MapFailure: the status and title come from StatusMapping, and the
            // person-facing sentence is resolved by code from ErrorMessages.resx, never from the
            // exception's own English message (that goes to the log below instead).
            DomainException domainEx => MapDomainException(domainEx, errorMessageResolver),

            ValidationException validationEx => new ErrorResponse(
                SharedErrorCodes.ValidationError,
                _localizer["ValidationFailed"],
                StatusCodes.Status400BadRequest,
                _localizer["ValidationErrorsOccurred"],
                validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToList())),

            UnauthorizedAccessException => new ErrorResponse(
                SharedErrorCodes.Unauthorized,
                _localizer["Unauthorized"],
                StatusCodes.Status401Unauthorized,
                _localizer["UnauthorizedDetail"]),

            KeyNotFoundException => new ErrorResponse(
                SharedErrorCodes.NotFound,
                _localizer["ResourceNotFound"],
                StatusCodes.Status404NotFound,
                _localizer["ResourceNotFoundDetail"]),

            // A genuine infrastructure-level signal, not a business refusal: the DbContext
            // (Infrastructure) translates EF's DbUpdateConcurrencyException to this
            // persistence-agnostic type when another writer saved the same aggregate first. 409,
            // not 400 or 500: the request was well-formed and nothing is broken, but the row the
            // caller read no longer matches what is stored, and retrying against the current row
            // is theirs to do.
            ConcurrencyConflictException => new ErrorResponse(
                SharedErrorCodes.Conflict,
                _localizer["ConcurrencyConflict"],
                StatusCodes.Status409Conflict,
                _localizer["ConcurrencyConflictDetail"]),

            // EF Core's default (non-retrying) execution strategy wraps a transient ADO.NET
            // failure, a dropped connection, a saturated pool, "too many clients already",
            // in InvalidOperationException ("An exception has been raised that is likely due
            // to a transient failure."). That is an infrastructure fault, not a client mistake:
            // matched ahead of the generic InvalidOperationException arm below so it reports
            // 500 per the documented mapping instead of misleading the caller with a 400
            // BUSINESS_RULE_VIOLATION for something entirely out of their control.
            InvalidOperationException { InnerException: DbException } => new ErrorResponse(
                SharedErrorCodes.InternalError,
                _localizer["InternalServerError"],
                StatusCodes.Status500InternalServerError,
                _localizer["UnexpectedErrorOccurred"]),

            InvalidOperationException => new ErrorResponse(
                SharedErrorCodes.BusinessRuleViolation,
                _localizer["BusinessRuleViolation"],
                StatusCodes.Status400BadRequest,
                _localizer["BusinessRuleViolationDetail"]),

            JsonException => new ErrorResponse(
                SharedErrorCodes.InvalidRequest,
                _localizer["InvalidRequestBody"],
                StatusCodes.Status400BadRequest,
                _localizer["InvalidRequestBodyDetail"]),

            BadHttpRequestException => new ErrorResponse(
                SharedErrorCodes.InvalidRequest,
                _localizer["InvalidRequestBody"],
                StatusCodes.Status400BadRequest,
                _localizer["InvalidRequestBodyDetail"]),

            _ => new ErrorResponse(
                SharedErrorCodes.InternalError,
                _localizer["InternalServerError"],
                StatusCodes.Status500InternalServerError,
                _localizer["UnexpectedErrorOccurred"]),
        };

        // Only an unmapped 500 is a genuine error worth surfacing. Mapped 4xx outcomes
        // (validation, auth, not found, bad request) are expected business failures: log them at
        // Debug so they stay out of the Warning+ observability store and do not bury real errors.
        if (response.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "An unhandled exception occurred");
        }
        else
        {
            _logger.LogDebug(exception, "Request rejected with {StatusCode} ({ErrorType})", response.Status, response.Type);
        }

        // Guard: if the response has already started (headers flushed), we cannot modify
        // status code or content type; just log and bail to avoid InvalidOperationException.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started, cannot write ProblemDetails for {ErrorType}", response.Type);
            return;
        }

        // RFC 9457 media type: the ErrorResponse shape (type/title/status/detail/errors)
        // is ProblemDetails-compatible.
        await ErrorBody.WriteAsync(context, response, context.RequestAborted);
    }

    /// <summary>
    ///     Maps a <see cref="DomainException"/> to its status and localized body, the same way
    ///     <see cref="BaseApiController.MapFailure"/> maps a handler's <c>Result</c> refusal.
    /// </summary>
    /// <param name="domainException">The aggregate's own thrown invariant.</param>
    /// <param name="errorMessageResolver">Resolves the exception's code to its localized sentence.</param>
    /// <returns>The body to write, carrying the status to answer with.</returns>
    private static ErrorResponse MapDomainException(
        DomainException domainException, IErrorMessageResolver errorMessageResolver)
    {
        var (status, title) = StatusMapping.For(domainException.Code);
        return new ErrorResponse(domainException.Code, title, status, errorMessageResolver.Resolve(domainException.Code));
    }
}
