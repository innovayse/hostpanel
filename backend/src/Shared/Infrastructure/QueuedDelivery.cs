using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine.Runtime.Handlers;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/*
 * One answer to "is this chain delivered from a queue?", asked by the retry policy and by the policy that
 * makes local queues durable, so the two can never disagree about which chains are retried and which
 * queues keep an inbox.
 *
 * A chain whose handler answers with Result or Result<T> is a request/response use case: a controller
 * reaches it with InvokeAsync, inline, and nothing ever puts it on a queue. A recurring job runs on the
 * in-memory recurring-jobs queue on purpose. Every other chain (domain events, integration events, the
 * commands published for later) is delivered from a queue that nobody waits on.
 */

/// <summary>Classifies handler chains by how their messages are delivered.</summary>
public static class QueuedDelivery
{
    /// <summary>Whether <paramref name="chain"/> is delivered from a queue rather than invoked inline or run as a recurring job.</summary>
    /// <param name="chain">The handler chain to classify; a separated per-handler chain is classified by its own handler.</param>
    /// <returns><see langword="true"/> when the chain's messages travel through a durable queue.</returns>
    public static bool IsQueued(HandlerChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

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
