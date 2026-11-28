using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.API.Plugins;

/// <summary>
/// Passes the progress of a plugin change to the caller as server-sent <c>progress</c> events on the request's own
/// response. A caller that asked for <c>text/event-stream</c> gets the stream opened at the first report and every report
/// after it; the controller then ends the stream with the outcome (<c>BaseApiController.HandleReported</c>). For any other
/// caller a report does nothing. Download reports are passed on only when the whole percentage moves (or every 256 KiB
/// when the registry declared no size), so a fast download does not flood the stream.
/// </summary>
public sealed class EventStreamPluginChangeProgress : IPluginChangeProgress
{
    /// <summary>The name of a progress event.</summary>
    public const string ProgressEvent = "progress";

    /// <summary>The bytes between two download reports when the registry declared no size.</summary>
    private const long UnsizedStepBytes = 256 * 1024;

    /// <summary>The percentage of the whole a download report must move by to be passed on.</summary>
    private const int PercentScale = 100;

    /// <summary>Where the last download report passed on is kept for the request: its whole percentage, or its byte step.</summary>
    private static readonly object LastStepKey = new();

    /// <summary>The request the change runs in.</summary>
    private readonly IHttpContextAccessor _http;

    /// <summary>The API's own JSON settings (camelCase, enums as names), so an event reads like any other answer.</summary>
    private readonly IOptions<JsonOptions> _json;

    /// <summary>Records that a caller stopped reading the progress while its change went on.</summary>
    private readonly ILogger<EventStreamPluginChangeProgress> _logger;

    /// <summary>Initializes the reporter.</summary>
    /// <param name="http">The request the change runs in.</param>
    /// <param name="json">The API's own JSON settings.</param>
    /// <param name="logger">Records that a caller stopped reading the progress while its change went on.</param>
    public EventStreamPluginChangeProgress(IHttpContextAccessor http, IOptions<JsonOptions> json, ILogger<EventStreamPluginChangeProgress> logger)
    {
        _http = http;
        _json = json;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task EnterAsync(PluginChangeStage stage, CancellationToken ct)
    {
        return WriteAsync(new PluginChangeProgressDto(stage));
    }

    /// <inheritdoc/>
    public Task DownloadedAsync(long bytesDone, long? bytesTotal, CancellationToken ct)
    {
        var context = _http.HttpContext;
        if (context is null)
        {
            return Task.CompletedTask;
        }

        var step = bytesTotal is > 0 ? bytesDone * PercentScale / bytesTotal.Value : bytesDone / UnsizedStepBytes;
        if (context.Items.TryGetValue(LastStepKey, out var last) && last is long previous && previous == step)
        {
            return Task.CompletedTask;
        }

        context.Items[LastStepKey] = step;
        return WriteAsync(new PluginChangeProgressDto(PluginChangeStage.Download, bytesDone, bytesTotal));
    }

    /// <summary>Opens the stream when the caller asked for one, and writes one progress event to it.</summary>
    /// <param name="progress">The progress to pass on.</param>
    /// <returns>A task that completes once the event has been flushed, or at once when the caller reads no stream.</returns>
    private async Task WriteAsync(PluginChangeProgressDto progress)
    {
        var context = _http.HttpContext;
        if (context is null || !EventStreamWriter.IsWanted(context.Request))
        {
            return;
        }

        var data = JsonSerializer.Serialize(progress, _json.Value.JsonSerializerOptions);
        try
        {
            // Written with the request's own abort token, never the change's: a caller that goes away ends the report, not the change.
            await EventStreamWriter.OpenAsync(context.Response, context.RequestAborted);
            await EventStreamWriter.WriteAsync(context.Response, new StreamedEvent(data, ProgressEvent), context.RequestAborted);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            // The caller left: nobody reads the progress any more, and the change must still finish (files, record, restart).
            _logger.LogInformation("A plugin change kept running after its caller stopped reading its progress.");
        }
    }
}
