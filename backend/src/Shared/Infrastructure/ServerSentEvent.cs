using System.Text;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>The server-sent event framing: the lines of an event and of a comment, each ended by a blank line.</summary>
public static class ServerSentEvent
{
    /// <summary>The comment line sent while a stream is silent; a client ignores it, a proxy sees traffic.</summary>
    public const string Heartbeat = ": keepalive\n\n";

    /// <summary>
    /// Refuses an event whose name or id holds a carriage return, a line feed or a NUL. The host does not trust the plugin
    /// SDK to have checked: a line break would end the field and let the rest be read as a field of its own (an injected
    /// event), and a NUL makes a client drop the id. Refusing, not stripping, is deliberate: a stripped name would quietly
    /// become a different event than the plugin meant, while a refusal ends the stream with the <c>error</c> event and
    /// shows the fault.
    /// </summary>
    /// <param name="streamed">The event.</param>
    /// <exception cref="ArgumentException">The name or the id holds a forbidden character.</exception>
    public static void EnsureValid(StreamedEvent streamed)
    {
        ArgumentNullException.ThrowIfNull(streamed);

        ThrowIfForbidden(streamed.Name, nameof(streamed.Name));
        ThrowIfForbidden(streamed.Id, nameof(streamed.Id));
    }

    /// <summary>Frames an event as <c>event</c>, <c>id</c> and <c>data</c> lines followed by a blank line.</summary>
    /// <param name="streamed">The event.</param>
    /// <returns>The text to write; every line break inside the data starts a new <c>data</c> line, so no data can end the event early.</returns>
    /// <exception cref="ArgumentException">The name or the id holds a carriage return, a line feed or a NUL.</exception>
    public static string Format(StreamedEvent streamed)
    {
        EnsureValid(streamed);

        var frame = new StringBuilder();
        if (streamed.Name is not null)
        {
            frame.Append("event: ").Append(streamed.Name).Append('\n');
        }

        if (streamed.Id is not null)
        {
            frame.Append("id: ").Append(streamed.Id).Append('\n');
        }

        // The three line break forms the format recognises; splitting on all of them keeps a lone carriage return from
        // ending the field in the client while the host believed it was inside it.
        foreach (var line in streamed.Data.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            frame.Append("data: ").Append(line).Append('\n');
        }

        return frame.Append('\n').ToString();
    }

    /// <summary>Throws when a single-line field holds a character that would end it or corrupt it.</summary>
    /// <param name="value">The field, or <see langword="null"/> when absent.</param>
    /// <param name="field">The field's name, for the message.</param>
    private static void ThrowIfForbidden(string? value, string field)
    {
        if (value is not null && value.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
        {
            throw new ArgumentException($"The event {field} must not contain a carriage return, a line feed or a NUL.", field);
        }
    }
}
