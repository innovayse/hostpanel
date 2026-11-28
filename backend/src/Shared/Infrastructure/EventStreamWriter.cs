using System.Text;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// The writing half of a <c>text/event-stream</c> response, shared by <see cref="EventStreamResult"/> (a stream a producer
/// enumerates) and by a use case that reports its progress while it runs (<see cref="EventStreamEndResult"/> then ends that
/// stream with the outcome). One place sets the stream headers and frames an event, so the two cannot drift.
/// </summary>
public static class EventStreamWriter
{
    /// <summary>Whether the caller asked for a stream: its <c>Accept</c> header names <c>text/event-stream</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when the caller reads server-sent events.</returns>
    public static bool IsWanted(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Headers.Accept.Any(value => value is not null && value.Contains(EventStreamResult.ContentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether this response is already a stream whose headers went out.</summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> once <see cref="OpenAsync"/> has started it.</returns>
    public static bool IsOpen(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.HasStarted && string.Equals(response.ContentType, EventStreamResult.ContentType, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sends the stream headers (<c>text/event-stream</c>, <c>Cache-Control: no-cache</c>, <c>X-Accel-Buffering: no</c>) and
    /// flushes them, so the client sees the stream open before the first event; a stream already open is left as it is.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <returns>A task that completes once the headers have been flushed.</returns>
    public static async Task OpenAsync(HttpResponse response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.HasStarted)
        {
            return;
        }

        response.ContentType = EventStreamResult.ContentType;
        response.Headers.CacheControl = "no-cache";

        // Tells a buffering reverse proxy to pass each write on at once.
        response.Headers["X-Accel-Buffering"] = "no";
        await response.StartAsync(ct);
        await response.Body.FlushAsync(ct);
    }

    /// <summary>Writes text that is already framed (an event or a comment line) and flushes it.</summary>
    /// <param name="response">The response.</param>
    /// <param name="text">The framed text.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <returns>A task that completes once the text has been flushed.</returns>
    public static async Task WriteAsync(HttpResponse response, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
        await response.Body.FlushAsync(ct);
    }

    /// <summary>Frames one event and writes it.</summary>
    /// <param name="response">The response, already open as a stream.</param>
    /// <param name="streamed">The event.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <returns>A task that completes once the event has been flushed.</returns>
    public static Task WriteAsync(HttpResponse response, StreamedEvent streamed, CancellationToken ct)
    {
        return WriteAsync(response, ServerSentEvent.Format(streamed), ct);
    }
}
