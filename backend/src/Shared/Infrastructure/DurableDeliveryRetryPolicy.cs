using FluentValidation;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.SharedKernel.Domain;
using JasperFx;
using JasperFx.CodeGeneration;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/*
 * Why this policy exists and why it skips so many chains.
 *
 * A message delivered from a durable local queue (a domain event, a published command) has nobody
 * waiting for it, so a transient fault (a database blip, a mail relay timing out) must be retried by
 * the bus, and a message that keeps failing must land in the dead-letter table where staff can see it,
 * not vanish. Those are the chains this policy covers.
 *
 * Two kinds of chain are left alone on purpose:
 * - Request/response chains, recognised by a handler that returns Result or Result<T>. A controller
 *   reaches them through InvokeAsync, and Wolverine runs RetryWithCooldown inline for InvokeAsync too,
 *   so a policy here would replay a user's command (a payment, an order) several times before the
 *   request answered. A command answers once; the caller decides whether to try again.
 * - Recurring jobs (IRecurringJob). Each pass schedules the next one itself and logs its own failure;
 *   a retried pass would run the job twice and could start a second chain.
 *
 * Messages with several handlers run under MultipleHandlerBehavior.Separated: Wolverine keeps the
 * message's own chain, now without handlers, and adds one chain per handler in HandlerChain.ByEndpoint,
 * each on its own local queue. Wolverine hands a handler policy that whole exploded list (every chain and
 * each of its ByEndpoint chains), so the per-handler chains get these rules here like any other, and a
 * retry replays only the handler that failed. The emptied parent passes the filter too; it never runs,
 * so the rules on it are inert.
 */

/// <summary>
/// Gives every queued, fire-and-forget handler chain the same failure rules: a deterministic refusal
/// goes straight to the dead-letter table, anything else is retried in place three times, then
/// rescheduled durably three times, then dead-lettered.
/// </summary>
public sealed class DurableDeliveryRetryPolicy : IHandlerPolicy
{
    /// <summary>
    /// Waits between the in-place retries: one, five and thirty seconds. Long enough for a dropped
    /// connection or a busy lock to clear, short enough that the message still holds its queue slot.
    /// </summary>
    private static readonly TimeSpan[] CooldownDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>
    /// Waits before each durable reschedule once the in-place retries are spent: five minutes, thirty
    /// minutes and two hours. A scheduled retry is stored in the database, so it survives a restart,
    /// and spans an outage of a dependency that lasts longer than a few seconds.
    /// </summary>
    private static readonly TimeSpan[] ScheduledDelays =
    [
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];

    /// <inheritdoc />
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (var chain in chains.Where(IsQueuedDelivery))
        {
            // Rules are matched in the order they are added, so the two deterministic refusals come
            // before the catch-all: a message that failed validation or broke a domain invariant fails
            // the same way on every retry, and retrying it only delays the dead letter.
            chain.OnException<ValidationException>().MoveToErrorQueue();
            chain.OnException<DomainException>().MoveToErrorQueue();

            chain.OnException<Exception>()
                .RetryWithCooldown(CooldownDelays)
                .Then.ScheduleRetry(ScheduledDelays)
                .Then.MoveToErrorQueue();
        }
    }

    /// <summary>
    /// Whether a chain is delivered from a queue rather than invoked by a waiting caller: its message
    /// is not a recurring job and none of its handlers answers with a <see cref="Result"/>.
    /// </summary>
    /// <param name="chain">The handler chain to classify.</param>
    /// <returns><see langword="true"/> when the chain should get the retry rules.</returns>
    private static bool IsQueuedDelivery(HandlerChain chain)
    {
        if (typeof(IRecurringJob).IsAssignableFrom(chain.MessageType))
        {
            return false;
        }

        return !chain.Handlers.Any(handler => AnswersWithResult(handler.Method.ReturnType));
    }

    /// <summary>
    /// Whether a handler method's return type is, or asynchronously yields, <see cref="Result"/> or
    /// <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="returnType">The handler method's declared return type.</param>
    /// <returns><see langword="true"/> for <c>Result</c>, <c>Result&lt;T&gt;</c> and a <c>Task</c> or <c>ValueTask</c> of either.</returns>
    private static bool AnswersWithResult(Type returnType)
    {
        var answered = returnType;
        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                answered = returnType.GetGenericArguments()[0];
            }
        }

        // Result<T> derives from Result, so one check covers both shapes.
        return typeof(Result).IsAssignableFrom(answered);
    }
}
