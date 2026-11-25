using FluentValidation;
using Innovayse.Hostpanel.Shared.Extensions;

namespace Innovayse.Hostpanel.Shared.Infrastructure.Observability;

/// <summary>
/// The logger <see cref="ExpectedFailureLoggerFactory"/> hands out: writes an Error caused by a
/// validation rejection, or by a cancellation the client caused by aborting the request, at Debug,
/// and everything else as given.
/// </summary>
public sealed class ExpectedFailureLogger : ILogger
{
    /// <summary>The logger that actually writes.</summary>
    private readonly ILogger _inner;

    /// <summary>Gives the request the logged exception belongs to, if the code runs inside one.</summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates the logger.</summary>
    /// <param name="inner">The logger that actually writes.</param>
    /// <param name="httpContextAccessor">Gives the request the logged exception belongs to.</param>
    public ExpectedFailureLogger(ILogger inner, IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var level = logLevel == LogLevel.Error && IsExpected(exception) ? LogLevel.Debug : logLevel;
        _inner.Log(level, eventId, state, exception, formatter);
    }

    /// <summary>
    /// Whether the exception, or any exception it wraps, is a validation rejection or a
    /// cancellation caused by the client aborting the current request.
    /// </summary>
    /// <param name="exception">The logged exception, if any.</param>
    /// <returns><c>true</c> for an expected failure.</returns>
    private bool IsExpected(Exception? exception)
    {
        var request = _httpContextAccessor.HttpContext;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ValidationException || request.IsClientAbort(current))
            {
                return true;
            }

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(IsExpected))
            {
                return true;
            }
        }

        return false;
    }
}
