namespace Innovayse.Application.Provisioning.Events;

using Innovayse.Domain.Services.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="ClientServiceCreatedEvent"/>.
/// Auto-provisioning is disabled — services stay in Pending status until
/// the client completes the setup wizard, which triggers provisioning explicitly.
/// </summary>
public sealed class ClientServiceCreatedHandler(ILogger<ClientServiceCreatedHandler> logger)
{
    /// <summary>
    /// Logs the service creation event without triggering provisioning.
    /// Provisioning is now client-driven via the setup wizard.
    /// </summary>
    /// <param name="evt">The domain event carrying the new service identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task HandleAsync(ClientServiceCreatedEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Service {ServiceId} created for client {ClientId}, product {ProductId} — awaiting client setup",
            evt.ServiceId, evt.ClientId, evt.ProductId);
        return Task.CompletedTask;
    }
}
