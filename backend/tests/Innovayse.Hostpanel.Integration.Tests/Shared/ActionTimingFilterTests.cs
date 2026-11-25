using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="ActionTimingFilter"/>'s failure line: MVC hands an action's exception back to the
/// filter instead of throwing it, so an action the client aborted must read as cancelled, not as a
/// failure, while a real exception from the action is still logged as one.
/// </summary>
public sealed class ActionTimingFilterTests
{
    /// <summary>
    /// Runs the filter around an action that completes with the exception, as MVC reports it.
    /// </summary>
    /// <param name="exception">The exception the action ended with.</param>
    /// <param name="requestAborted">Whether the request was aborted by the client.</param>
    /// <returns>The lines the filter logged.</returns>
    private static async Task<List<LogEntry>> RunAsync(Exception exception, bool requestAborted)
    {
        var provider = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(
            builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
        var filter = new ActionTimingFilter(
            loggerFactory.CreateLogger<ActionTimingFilter>(), Options.Create(new ActionTimingOptions()));

        using var aborted = new CancellationTokenSource();
        if (requestAborted)
        {
            aborted.Cancel();
        }

        var http = new DefaultHttpContext { RequestAborted = aborted.Token };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        await filter.OnActionExecutionAsync(
            executing,
            () => Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object())
            {
                Exception = exception,
            }));

        return provider.Entries.ToList();
    }

    /// <summary>An action the client aborted is logged once at Information, with no exception.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AbortedAction_IsLoggedAsCancelledWithoutErrorAsync()
    {
        var entries = await RunAsync(new OperationCanceledException(), requestAborted: true);

        entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
        entries.Should().ContainSingle(entry => entry.Message.StartsWith("ACTION CANCELLED"))
            .Which.Exception.Should().BeNull();
        entries.Should().NotContain(entry => entry.Message.StartsWith("ACTION FAILED"));
    }

    /// <summary>A cancellation while the request is still alive stays a failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellationOfLiveRequest_IsLoggedAsFailedAsync()
    {
        var entries = await RunAsync(new OperationCanceledException(), requestAborted: false);

        entries.Should().ContainSingle(entry => entry.Message.StartsWith("ACTION FAILED"))
            .Which.Level.Should().Be(LogLevel.Error);
    }

    /// <summary>A real exception from the action stays a failure, even when the request was aborted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ActionException_IsLoggedAsFailedAsync()
    {
        var entries = await RunAsync(new InvalidOperationException("boom"), requestAborted: true);

        var failed = entries.Should().ContainSingle(entry => entry.Message.StartsWith("ACTION FAILED")).Subject;
        failed.Level.Should().Be(LogLevel.Error);
        failed.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
