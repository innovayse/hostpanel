using System.Diagnostics;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Shared.Filters;

/// <summary>
/// Times every controller action and logs its start, completion (Warning when slow) or failure.
/// Only the operation name, the duration, the status and the exception object are logged: never
/// a body, header, route value or query string, because prompts and tokens travel in them.
/// </summary>
public sealed class ActionTimingFilter : IAsyncActionFilter
{
    /// <summary>Where the timing lines go.</summary>
    private readonly ILogger<ActionTimingFilter> _logger;

    /// <summary>The enabled switch and the slow threshold.</summary>
    private readonly IOptions<ActionTimingOptions> _options;

    /// <summary>Creates the filter.</summary>
    /// <param name="logger">Where the timing lines go.</param>
    /// <param name="options">The enabled switch and the slow threshold.</param>
    public ActionTimingFilter(ILogger<ActionTimingFilter> logger, IOptions<ActionTimingOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>Runs the action, timing it, and logs the outcome.</summary>
    /// <param name="context">The action about to run.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action has run.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            await next();
            return;
        }

        var operation = OperationName(context);

        // Debug, not Information: the completed line already says the action ran, so the start
        // line is only useful when chasing a hang.
        _logger.LogDebug("ACTION START: {Operation}", operation);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var executed = await next();
            var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                // MVC hands an action's exception back here instead of throwing it, so a request the
                // client aborted (a reload) arrives on this path as well as in the catch below.
                if (context.HttpContext.IsClientAbort(executed.Exception))
                {
                    LogCancelled(operation, elapsedMs);
                    return;
                }

                _logger.LogError(
                    executed.Exception, "ACTION FAILED: {Operation} in {ElapsedMs}ms", operation, elapsedMs);
                return;
            }

            var statusCode = StatusCodeOf(executed);
            if (elapsedMs > options.SlowThresholdMs)
            {
                _logger.LogWarning(
                    "ACTION COMPLETED: {Operation} in {ElapsedMs}ms (SLOW) with {StatusCode}",
                    operation,
                    elapsedMs,
                    statusCode);
            }
            else
            {
                _logger.LogInformation(
                    "ACTION COMPLETED: {Operation} in {ElapsedMs}ms with {StatusCode}",
                    operation,
                    elapsedMs,
                    statusCode);
            }
        }
        catch (Exception exception) when (context.HttpContext.IsClientAbort(exception))
        {
            LogCancelled(operation, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _logger.LogError(exception, "ACTION FAILED: {Operation} in {ElapsedMs}ms", operation, elapsedMs);
            throw;
        }
    }

    /// <summary>
    /// Logs an action the client aborted: an expected outcome, not a server failure, so one
    /// Information line and no exception.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="elapsedMs">Milliseconds from the start of the action.</param>
    private void LogCancelled(string operation, long elapsedMs)
    {
        _logger.LogInformation("ACTION CANCELLED: {Operation} in {ElapsedMs}ms", operation, elapsedMs);
    }

    /// <summary>`{Controller}.{Action}`, the controller named without its `Controller` suffix.</summary>
    /// <param name="context">The action about to run.</param>
    /// <returns>The operation name.</returns>
    private static string OperationName(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            return $"{descriptor.ControllerName}.{descriptor.ActionName}";
        }

        return context.ActionDescriptor.DisplayName ?? "Unknown.Unknown";
    }

    /// <summary>The status the action answered with, when it can be known yet.</summary>
    /// <param name="executed">The executed action.</param>
    /// <returns>The status code.</returns>
    private static int StatusCodeOf(ActionExecutedContext executed)
    {
        if (executed.Result is IStatusCodeActionResult { StatusCode: { } status })
        {
            return status;
        }

        return executed.HttpContext.Response.StatusCode;
    }
}
