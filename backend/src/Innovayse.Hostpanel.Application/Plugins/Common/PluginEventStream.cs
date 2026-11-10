using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Endpoints;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Plugins.Common.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Turns the events of a plugin's streamed reply into the host's, and keeps what happens inside the plugin from
/// reaching the caller. The status is already 200 when a stream fails, so a plugin that throws, or emits an event the
/// host refuses (a name or id with a control character, a reserved name, data over the cap), is an <c>error</c> event
/// (<c>code</c>, the localized <c>message</c>, <c>partial</c>) followed by <c>done</c>, never its exception.
/// </summary>
public static class PluginEventStream
{
    /// <summary>The names the host writes itself (<c>error</c>, <c>done</c>), which a plugin may not use.</summary>
    private static readonly HashSet<string> ReservedNames = Enum.GetValues<PluginStreamEvent>()
        .Select(e => e.ToString().ToLowerInvariant())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Forwards the plugin's events one by one, as the response asks for them.</summary>
    /// <param name="pluginId">The plugin that answered, for the log.</param>
    /// <param name="events">The plugin's events; enumerated with <paramref name="ct"/>, which is how the plugin learns the caller left.</param>
    /// <param name="failedMessage">The sentence of the <c>error</c> event, already in the caller's language.</param>
    /// <param name="logger">Records a plugin that failed, with its id.</param>
    /// <param name="maxEventBytes">The most UTF-8 bytes the data of one event may carry.</param>
    /// <param name="ct">Cancelled when the caller disconnects; it is the token handed to the plugin's enumerator.</param>
    /// <returns>The events, ending with <c>error</c> and <c>done</c> when the plugin failed.</returns>
    public static async IAsyncEnumerable<StreamedEvent> TranslateAsync(
        string pluginId,
        IAsyncEnumerable<PluginEvent> events,
        string failedMessage,
        ILogger logger,
        int maxEventBytes,
        [EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerator<PluginEvent>? source = null;
        Exception? failure = null;
        try
        {
            // A yield is not allowed inside a catch, so every failure is recorded here and answered after the loop.
            try
            {
                source = events.GetAsyncEnumerator(ct);
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                failure = ex;
            }

            while (failure is null)
            {
                StreamedEvent? next = null;
                try
                {
                    if (!await source!.MoveNextAsync())
                    {
                        break;
                    }

                    next = Admit(source.Current, maxEventBytes);
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    failure = ex;
                }

                if (next is not null)
                {
                    yield return next;
                }
            }

            // Disposed here as well, so a plugin whose disposal throws still ends the stream with the error events.
            if (source is not null)
            {
                var closing = source;
                source = null;
                try
                {
                    await closing.DisposeAsync();
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    failure ??= ex;
                }
            }
        }
        finally
        {
            // Reached with a live enumerator only when the consumer stopped early; nothing can be answered then.
            if (source is not null)
            {
                try
                {
                    await source.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Plugin {PluginId} failed while its abandoned stream was being closed.", pluginId);
                }
            }
        }

        if (failure is not null)
        {
            // Nothing of the exception reaches the caller; the log is where an operator reads it.
            logger.LogError(failure, "Plugin {PluginId} failed while streaming its reply.", pluginId);
            yield return HostEvent(PluginStreamEvent.Error, new { code = HostpanelErrorCodes.PLUGIN_FAILED, message = failedMessage, partial = true });
            yield return HostEvent(PluginStreamEvent.Done, new { });
        }
    }

    /// <summary>The events that end a stream whose plugin stayed silent past the call time limit: <c>error</c> then <c>done</c>.</summary>
    /// <param name="timeoutMessage">The sentence of the <c>error</c> event, already in the caller's language.</param>
    /// <returns>The two events, with no text of the plugin.</returns>
    public static IReadOnlyList<StreamedEvent> TimeoutEvents(string timeoutMessage)
    {
        return
        [
            HostEvent(PluginStreamEvent.Error, new { code = HostpanelErrorCodes.PLUGIN_TIMEOUT, message = timeoutMessage, partial = true }),
            HostEvent(PluginStreamEvent.Done, new { }),
        ];
    }

    /// <summary>Checks one event of the plugin against what the host allows and turns it into the host's event.</summary>
    /// <param name="current">The plugin's event.</param>
    /// <param name="maxEventBytes">The most UTF-8 bytes the data may carry.</param>
    /// <returns>The event to write.</returns>
    /// <exception cref="InvalidOperationException">The name is reserved, or the data is over the cap.</exception>
    /// <exception cref="ArgumentException">The name or the id holds a carriage return, a line feed or a NUL.</exception>
    private static StreamedEvent Admit(PluginEvent current, int maxEventBytes)
    {
        var streamed = new StreamedEvent(current.Data, current.Name, current.Id);
        ServerSentEvent.EnsureValid(streamed);

        if (current.Name is not null && ReservedNames.Contains(current.Name))
        {
            throw new InvalidOperationException($"A plugin may not emit the host's event name '{current.Name}'.");
        }

        if (Encoding.UTF8.GetByteCount(current.Data) > maxEventBytes)
        {
            throw new InvalidOperationException($"A plugin event carries more than the {maxEventBytes} bytes of data allowed.");
        }

        return streamed;
    }

    /// <summary>Builds an event the host writes itself.</summary>
    /// <param name="name">Which event.</param>
    /// <param name="data">The value written as camelCase JSON.</param>
    /// <returns>The event.</returns>
    private static StreamedEvent HostEvent(PluginStreamEvent name, object data)
    {
        return new StreamedEvent(JsonSerializer.Serialize(data, JsonSerializerOptions.Web), name.ToString().ToLowerInvariant());
    }
}
