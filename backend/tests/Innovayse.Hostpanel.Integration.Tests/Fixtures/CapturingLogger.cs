using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps every formatted message, so a test can assert
/// which precise reason an adapter logged for a refusal it deliberately does not return.
/// </summary>
/// <typeparam name="T">The category the logger stands in for.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>Gets every message logged so far, formatted, in order.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>Gets every entry logged so far with its level, in order, for a test that asserts how loudly something was logged.</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

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
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Messages.Add(message);
        Entries.Add((logLevel, message));
    }
}
