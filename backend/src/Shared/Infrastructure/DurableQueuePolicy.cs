using Microsoft.Extensions.DependencyInjection;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;
using Wolverine.Transports.Local;
using Endpoint = Wolverine.Configuration.Endpoint;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/*
 * Why only some local queues are durable.
 *
 * Wolverine creates a local queue for every handled message type, and a further one per handler when an
 * event has several (MultipleHandlerBehavior.Separated). Most of those types are commands and queries a
 * controller invokes inline: their queue is never used. Making every queue durable gave each of them a
 * durable receiver with its inbox batching, its retry blocks and its timers, several hundred per host,
 * for queues that never carry a message. The integration suite's test hosts held that weight per derived
 * host and three shards that fit in 5 GB on develop ran out of memory.
 *
 * So a queue keeps an inbox only when a chain it serves is delivered from a queue (QueuedDelivery, the
 * same rule the retry policy uses): a queue for messages someone publishes and nobody waits on. Every
 * other local queue stays Wolverine's in-memory default, exactly as before durable messaging, and the
 * recurring-jobs queue stays in memory by its own explicit setting, applied after endpoint policies.
 */

/// <summary>
/// Makes a local queue durable when any handler chain it serves is delivered from a queue, and leaves
/// every other local queue in memory.
/// </summary>
public sealed class DurableQueuePolicy : IEndpointPolicy
{
    /// <inheritdoc />
    public void Apply(Endpoint endpoint, IWolverineRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(runtime);

        if (endpoint is not LocalQueue queue)
        {
            return;
        }

        if (ServedChains(queue, runtime).Any(QueuedDelivery.IsQueued))
        {
            queue.Mode = EndpointMode.Durable;
        }
    }

    /// <summary>
    /// The chains a local queue delivers to: those whose message type the conventional routing names after
    /// this queue (<see cref="LocalQueueNames"/> is the namer the routing is configured with), and the separated
    /// per-handler chains that list it as their endpoint.
    /// </summary>
    /// <param name="queue">The local queue being configured.</param>
    /// <param name="runtime">The runtime whose handler graph is read.</param>
    /// <returns>Every chain the queue can hand a message to; empty for a queue nothing is routed to.</returns>
    private static IEnumerable<HandlerChain> ServedChains(LocalQueue queue, IWolverineRuntime runtime)
    {
        // The graph is read from the runtime's container: an endpoint policy is handed the runtime, not its
        // services, and the graph is built and compiled before any endpoint is.
        var chains = runtime.Services.GetRequiredService<HandlerGraph>().Chains;

        var named = chains.Where(chain => chain.Handlers.Count > 0
            && string.Equals(LocalQueueNames.For(chain.MessageType), queue.EndpointName, StringComparison.Ordinal));

        var separated = chains
            .SelectMany(chain => chain.ByEndpoint)
            .Where(chain => chain.Endpoints.Contains(queue));

        return named.Concat(separated);
    }
}
