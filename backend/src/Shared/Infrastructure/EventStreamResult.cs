using System.Diagnostics;
using System.Text;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Writes a <see cref="StreamedReply"/> as <c>text/event-stream</c>: the headers first, then every event as it is produced,
/// flushed at once, with a comment line whenever the stream has been silent for the reply's heartbeat. The events are
/// enumerated with a token linked to the request abort token and cancelled on every way out, so a client that goes away,
/// a failed write and a normal end all cancel the producer. When the reply has an idle limit, a producer that sends nothing
/// for that long is cancelled and the client is told with the limit's end events; heartbeats do not count as activity. The request always ends: waiting for a producer that ignores
/// its token is bounded.
/// </summary>
public sealed class EventStreamResult : IActionResult
{
    /// <summary>The media type of a server-sent event stream.</summary>
    public const string ContentType = "text/event-stream";

    /// <summary>How long the end of the request waits for a step the producer is still running after it was cancelled.</summary>
    public static readonly TimeSpan DefaultSettleTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The reply to write.</summary>
    private readonly StreamedReply _reply;

    /// <summary>How long to wait for a step still in flight once the stream is over.</summary>
    private readonly TimeSpan _settleTimeout;

    /// <summary>Initializes the result with the default settle wait.</summary>
    /// <param name="reply">The reply to write.</param>
    public EventStreamResult(StreamedReply reply)
        : this(reply, DefaultSettleTimeout)
    {
    }

    /// <summary>Initializes the result.</summary>
    /// <param name="reply">The reply to write.</param>
    /// <param name="settleTimeout">How long to wait for a producer step still in flight when the stream is over.</param>
    public EventStreamResult(StreamedReply reply, TimeSpan settleTimeout)
    {
        ArgumentNullException.ThrowIfNull(reply);

        _reply = reply;
        _settleTimeout = settleTimeout;
    }

    /// <summary>Writes the stream until it ends, fails or the client disconnects.</summary>
    /// <param name="context">The action context of the request.</param>
    /// <returns>A task that completes when the stream has ended.</returns>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var http = context.HttpContext;
        var logger = http.RequestServices?.GetService<ILogger<EventStreamResult>>() ?? NullLogger<EventStreamResult>.Instance;

        // The producer's token: it is cancelled by the client leaving and, in the finally, by every other way out.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        var ct = lifetime.Token;

        var headers = http.Response.Headers;
        http.Response.ContentType = ContentType;
        headers.CacheControl = "no-cache";

        // Tells a buffering reverse proxy to pass each write on at once.
        headers["X-Accel-Buffering"] = "no";

        IAsyncEnumerator<StreamedEvent>? events = null;
        Task<bool>? pending = null;
        try
        {
            events = _reply.Events.GetAsyncEnumerator(ct);

            // Sent now, so the client sees the stream open before the first event exists.
            await http.Response.StartAsync(ct);
            await http.Response.Body.FlushAsync(ct);

            // The producer's own silence: restarted by each of its events, never by the host's heartbeat.
            var idle = Stopwatch.StartNew();
            var limit = _reply.IdleLimit;

            pending = events.MoveNextAsync().AsTask();
            while (true)
            {
                var wait = _reply.Heartbeat;
                if (limit is not null)
                {
                    wait = TimeSpan.FromTicks(Math.Min(wait.Ticks, Math.Max(0, (limit.Timeout - idle.Elapsed).Ticks)));
                }

                using var silence = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var finished = await Task.WhenAny(pending, Task.Delay(wait, silence.Token));

                // The event may have arrived just as the wait ran out: it is then used, not cut.
                if (finished != pending && !pending.IsCompleted)
                {
                    if (limit is not null && idle.Elapsed >= limit.Timeout)
                    {
                        // The producer is cancelled in the finally; the client is told why the stream ended.
                        limit.OnExpired();
                        foreach (var end in limit.EndEvents)
                        {
                            await WriteAsync(http, ServerSentEvent.Format(end), ct);
                        }

                        return;
                    }

                    await WriteAsync(http, ServerSentEvent.Heartbeat, ct);
                    continue;
                }

                await silence.CancelAsync();
                if (!await pending)
                {
                    return;
                }

                await WriteAsync(http, ServerSentEvent.Format(events.Current), ct);
                idle.Restart();
                pending = events.MoveNextAsync().AsTask();
            }
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // The client left: not a failure, there is nobody to answer.
        }
        finally
        {
            await lifetime.CancelAsync();
            var settled = await SettleAsync(pending, logger);
            await CloseAsync(events, settled, logger);
            await ReleaseOwnerAsync(pending, settled, logger);
        }
    }

    /// <summary>
    /// Disposes what the producer held for the stream (<see cref="StreamedReply.Owner"/>) once the producer is over. When a
    /// step was abandoned it is still running and may still use what the owner holds, so the owner is disposed when that step
    /// ends instead of under it; the request does not wait for it.
    /// </summary>
    /// <param name="pending">The last step of the producer, or <see langword="null"/> when none was started.</param>
    /// <param name="settled">Whether no step is running any more.</param>
    /// <param name="logger">Records a disposal that failed.</param>
    /// <returns>A task that completes when the owner has been disposed, or its disposal has been deferred to the step's end.</returns>
    private async Task ReleaseOwnerAsync(Task<bool>? pending, bool settled, ILogger logger)
    {
        if (_reply.Owner is not { } owner)
        {
            return;
        }

        if (settled || pending is null)
        {
            await DisposeOwnerAsync(owner, logger);
            return;
        }

        _ = pending.ContinueWith(
            async _ => await DisposeOwnerAsync(owner, logger),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>Disposes the owner of a stream, logging rather than throwing a failure: the stream is already over.</summary>
    /// <param name="owner">The owner to dispose.</param>
    /// <param name="logger">Records a disposal that failed.</param>
    /// <returns>A task that completes when the owner has been disposed.</returns>
    private static async Task DisposeOwnerAsync(IAsyncDisposable owner, ILogger logger)
    {
        try
        {
            await owner.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "What a streamed reply's producer held failed while it was being released.");
        }
    }

    /// <summary>Writes text to the response and flushes it.</summary>
    /// <param name="http">The request's context.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <returns>A task that completes once the text has been flushed.</returns>
    private static async Task WriteAsync(HttpContext http, string text, CancellationToken ct)
    {
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
        await http.Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Waits, for a bounded time, for a step the producer is still running. An enumerator must not be disposed while a step
    /// is in flight, and a step that was cancelled or failed has already been dealt with: only its end matters here. A
    /// producer that ignores its token is abandoned after the timeout, so the request, and the lease it holds, can end.
    /// </summary>
    /// <param name="pending">The step in flight, or <see langword="null"/> when none was started.</param>
    /// <param name="logger">Records an abandoned step.</param>
    /// <returns><see langword="true"/> when no step is running any more; <see langword="false"/> when one was abandoned.</returns>
    private async Task<bool> SettleAsync(Task<bool>? pending, ILogger logger)
    {
        if (pending is null)
        {
            return true;
        }

        using var timeout = new CancellationTokenSource();
        var finished = await Task.WhenAny(pending, Task.Delay(_settleTimeout, timeout.Token));
        if (finished != pending)
        {
            logger.LogWarning(
                "A streamed reply's producer ignored its cancellation; the step was abandoned after {Timeout}.", _settleTimeout);

            // Observed, so the abandoned step's eventual failure is not reported as unobserved.
            _ = pending.ContinueWith(
                static step => _ = step.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return false;
        }

        await timeout.CancelAsync();
        try
        {
            await pending;
        }
        catch (Exception)
        {
            // The producer's own failure of a step nobody reads any more; the stream is already over.
        }

        return true;
    }

    /// <summary>Disposes the enumerator unless a step of it is still running, which it must not be disposed under.</summary>
    /// <param name="events">The enumerator, or <see langword="null"/> when it could not be created.</param>
    /// <param name="settled">Whether no step is running.</param>
    /// <param name="logger">Records a disposal that failed.</param>
    /// <returns>A task that completes when the enumerator has been disposed or left alone.</returns>
    private static async Task CloseAsync(IAsyncEnumerator<StreamedEvent>? events, bool settled, ILogger logger)
    {
        if (events is null || !settled)
        {
            return;
        }

        try
        {
            await events.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A streamed reply's producer failed while it was being closed.");
        }
    }
}
