using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/*
 * Why events are published in tests, not invoked.
 *
 * The host runs Wolverine with MultipleHandlerBehavior.Separated: an event with several handlers has one
 * chain per handler and its own chain has none, so InvokeAsync on it throws NoHandlerForEndpointException.
 * Publishing is also what production does (the domain-event interceptor writes events to the outbox), so a
 * test that publishes exercises the same routing, durable queues and retry rules.
 *
 * Wolverine's tracked session waits until every message the publish caused has been handled, including
 * the commands its handlers dispatch. A scheduled message counts as finished when it is scheduled, so a
 * cooldown or a durable reschedule never holds the wait open. Recurring jobs are left out on purpose: they
 * run on their own in-memory schedule in every host, a pass may start while a test waits, and its outcome
 * is not the test's.
 */

/// <summary>
/// Publishes a message on a test host and waits until everything it set off has been handled, so a test
/// can assert on the outcome right after the call.
/// </summary>
public static class MessageDispatch
{
    /// <summary>
    /// How long a publish may take to settle when the caller names no timeout. Generous because the first
    /// message of a host compiles its handler chains in memory, which takes seconds on a busy machine.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Publishes <paramref name="message"/> and waits until every handler it reached, and every message
    /// those handlers sent in turn, has finished.
    /// </summary>
    /// <param name="services">The host's root provider or a scope's provider; either reaches the host's bus.</param>
    /// <param name="message">The event or command to publish.</param>
    /// <param name="timeout">How long to wait before failing; <see langword="null"/> takes the 30-second default.</param>
    /// <returns>The tracked session, for a test that asserts on what was sent or handled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="message"/> is null.</exception>
    /// <exception cref="TimeoutException">The activity did not settle within the timeout.</exception>
    /// <exception cref="AggregateException">A handler reached by the publish threw.</exception>
    public static Task<ITrackedSession> PublishAndWaitAsync(
        this IServiceProvider services,
        object message,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(message);

        return services.GetRequiredService<IHost>()
            .TrackActivity()
            .Timeout(timeout ?? DefaultTimeout)
            .IgnoreMessagesMatchingType(IsRecurringJob)
            .PublishMessageAndWaitAsync(message);
    }

    /// <summary>Whether a message type is a recurring job, whose schedule runs beside every test.</summary>
    /// <param name="messageType">The type of a message the session saw.</param>
    /// <returns><see langword="true"/> for an <see cref="IRecurringJob"/>.</returns>
    private static bool IsRecurringJob(Type messageType) => typeof(IRecurringJob).IsAssignableFrom(messageType);
}
