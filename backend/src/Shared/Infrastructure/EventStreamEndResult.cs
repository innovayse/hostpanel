using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Ends a stream a use case opened while it reported its progress: writes the one last event that carries the outcome
/// (<c>done</c> with the result, or <c>failed</c> with the standard error body) and lets the response complete. The
/// status is already 200 and the headers are out, which is why a failure travels as an event here and not as a status.
/// </summary>
public sealed class EventStreamEndResult : IActionResult
{
    /// <summary>The name of the event that carries a success.</summary>
    public const string DoneEvent = "done";

    /// <summary>The name of the event that carries a refusal, with the standard error body as its data.</summary>
    public const string FailedEvent = "failed";

    /// <summary>The last event.</summary>
    private readonly StreamedEvent _last;

    /// <summary>Initializes the result.</summary>
    /// <param name="last">The last event.</param>
    public EventStreamEndResult(StreamedEvent last)
    {
        ArgumentNullException.ThrowIfNull(last);

        _last = last;
    }

    /// <summary>Writes the last event to the open stream.</summary>
    /// <param name="context">The action context of the request.</param>
    /// <returns>A task that completes once the event has been flushed.</returns>
    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var http = context.HttpContext;
        return EventStreamWriter.WriteAsync(http.Response, _last, http.RequestAborted);
    }
}
