using Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedServiceHostingResources;
using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// When a service is terminated, dispatches <see cref="RemoveTerminatedServiceHostingResourcesCommand"/>, which marks the
/// websites and databases plugins held on it as removed. A separate handler beside <see cref="ClientServiceTerminatedHandler"/>,
/// so a failure of one settlement never stops the other. A non-notification handler, and safe as one: it only triggers
/// that idempotent command, whatever path terminated the service.
/// </summary>
public sealed class RemoveHostingResourcesOnServiceTerminatedHandler
{
    /// <summary>Dispatches the command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a failure, so the leftovers can be settled by hand.</summary>
    private readonly ILogger<RemoveHostingResourcesOnServiceTerminatedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Dispatches the command.</param>
    /// <param name="logger">Records a failure, so the leftovers can be settled by hand.</param>
    public RemoveHostingResourcesOnServiceTerminatedHandler(
        IMessageBus bus, ILogger<RemoveHostingResourcesOnServiceTerminatedHandler> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles <see cref="ClientServiceTerminatedEvent"/>.</summary>
    /// <param name="evt">The termination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the command ran.</returns>
    public async Task HandleAsync(ClientServiceTerminatedEvent evt, CancellationToken ct)
    {
        try
        {
            var outcome = await _bus.InvokeAsync<Result>(new RemoveTerminatedServiceHostingResourcesCommand(evt.ServiceId), ct);
            if (outcome.IsFailure)
            {
                _logger.LogWarning(
                    "Settling the hosting resources of terminated service {ServiceId} was refused with {ErrorCode}.",
                    evt.ServiceId,
                    outcome.ErrorCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Wolverine combines every handler of one message into a single chain, so a throw here
            // would also stop the other handlers of the termination.
            _logger.LogError(ex, "Settling the hosting resources of terminated service {ServiceId} failed.", evt.ServiceId);
        }
    }
}
