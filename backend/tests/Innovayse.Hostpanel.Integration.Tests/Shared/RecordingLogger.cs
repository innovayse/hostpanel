using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>Records into the provider's queue.</summary>
/// <param name="entries">The queue to record into.</param>
internal sealed class RecordingLogger(ConcurrentQueue<LogEntry> entries) : ILogger
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
}
