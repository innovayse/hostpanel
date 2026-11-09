using Innovayse.Hostpanel.Domain.Services.Events;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Provisioning.Events;

/// <summary>
/// Handles <see cref="ClientServiceCreatedEvent"/>.
/// Auto-provisioning is disabled: services stay in Pending status until
/// the client completes the setup wizard, which triggers provisioning explicitly.
/// </summary>
public sealed class ClientServiceCreatedHandler
{
    /// <summary>Records that a service was created, since no provisioning follows automatically.</summary>
    private readonly ILogger<ClientServiceCreatedHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="ClientServiceCreatedHandler"/>.</summary>
    /// <param name="logger">Records that a service was created, since no provisioning follows automatically.</param>
    public ClientServiceCreatedHandler(ILogger<ClientServiceCreatedHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs the service creation event without triggering provisioning.
    /// Provisioning is now client-driven via the setup wizard.
    /// </summary>
    /// <param name="evt">The domain event carrying the new service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(ClientServiceCreatedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Service {ServiceId} created for client {ClientId}, product {ProductId}, awaiting client setup",
            evt.ServiceId, evt.ClientId, evt.ProductId);
        return Task.CompletedTask;
    }
}
