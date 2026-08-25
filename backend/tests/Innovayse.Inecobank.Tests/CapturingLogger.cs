namespace Innovayse.Inecobank.Tests;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ILogger"/> implementation that records every formatted message and scope it
/// receives, without asserting on them itself — used to prove sensitive values (merchant
/// credentials) never reach the logger, no matter which log call or overload writes them.
/// </summary>
public sealed class CapturingLogger : ILogger
{
    /// <summary>Gets every message formatted through <see cref="Log{TState}"/>, in order.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>Gets every scope state's string form passed to <see cref="BeginScope{TState}"/>, in order.</summary>
    public List<string> Scopes { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        Scopes.Add(state.ToString() ?? string.Empty);
        return NullScope.Instance;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        if (exception is not null)
        {
            // The exception's own message/ToString() is a distinct channel a credential could
            // leak through (e.g. an HTTP client embedding the request body in its message).
            Messages.Add(exception.ToString());
        }
    }

    /// <summary>No-op <see cref="IDisposable"/> returned by <see cref="BeginScope{TState}"/>.</summary>
    private sealed class NullScope : IDisposable
    {
        /// <summary>Gets the shared singleton instance.</summary>
        public static readonly NullScope Instance = new();

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
