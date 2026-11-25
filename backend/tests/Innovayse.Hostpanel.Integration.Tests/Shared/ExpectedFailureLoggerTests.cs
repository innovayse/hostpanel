using FluentValidation;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="ExpectedFailureLogger"/> as <see cref="ExpectedFailureLoggingExtensions"/> registers
/// it: the Error line Wolverine writes for a request the client aborted is written at Debug, where
/// it would otherwise read as a server fault, while every other exception, a cancellation of any
/// other cause included, stays an Error.
/// </summary>
public sealed class ExpectedFailureLoggerTests
{
    /// <summary>
    /// Logs one Error carrying the exception through a logger built the way the API registers it.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="requestAborted">Whether the current request was aborted, or <c>null</c> for no request.</param>
    /// <returns>The lines the inner provider recorded.</returns>
    private static List<LogEntry> LogError(Exception exception, bool? requestAborted)
    {
        var provider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
        services.AddExpectedFailureLogDemotion();

        using var root = services.BuildServiceProvider();
        using var aborted = new CancellationTokenSource();
        if (requestAborted is not null)
        {
            if (requestAborted.Value)
            {
                aborted.Cancel();
            }

            root.GetRequiredService<IHttpContextAccessor>().HttpContext =
                new DefaultHttpContext { RequestAborted = aborted.Token };
        }

        root.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Wolverine.Runtime")
            .Log(LogLevel.Error, new EventId(0), "Invocation of X failed!", exception, (state, _) => state);

        return provider.Entries.ToList();
    }

    /// <summary>A cancellation of an aborted request is written at Debug, not Error.</summary>
    [Fact]
    public void CancellationOfAbortedRequest_IsWrittenAtDebug()
    {
        var entries = LogError(new OperationCanceledException(), requestAborted: true);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Debug);
    }

    /// <summary>A cancellation wrapped by another exception is still told apart from a fault.</summary>
    [Fact]
    public void WrappedCancellationOfAbortedRequest_IsWrittenAtDebug()
    {
        var entries = LogError(
            new InvalidOperationException("wrapped", new TaskCanceledException()), requestAborted: true);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Debug);
    }

    /// <summary>A cancellation while the request is still alive, such as a timeout, stays an Error.</summary>
    [Fact]
    public void CancellationOfLiveRequest_StaysError()
    {
        var entries = LogError(new OperationCanceledException(), requestAborted: false);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Error);
    }

    /// <summary>A cancellation outside any request, such as a host that is stopping, stays an Error.</summary>
    [Fact]
    public void CancellationOutsideRequest_StaysError()
    {
        var entries = LogError(new OperationCanceledException(), requestAborted: null);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Error);
    }

    /// <summary>A real handler exception stays an Error even when the request was aborted.</summary>
    [Fact]
    public void HandlerExceptionOfAbortedRequest_StaysError()
    {
        var entries = LogError(new InvalidOperationException("boom"), requestAborted: true);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Error);
        entries[0].Exception.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>A validation rejection is still written at Debug.</summary>
    [Fact]
    public void ValidationRejection_IsWrittenAtDebug()
    {
        var entries = LogError(new ValidationException("rejected"), requestAborted: false);

        entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Debug);
    }
}
