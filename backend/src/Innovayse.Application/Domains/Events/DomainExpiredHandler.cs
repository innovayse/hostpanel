namespace Innovayse.Application.Domains.Events;

using Innovayse.Domain.Domains.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="DomainExpiredEvent"/> raised when a domain transitions to the Expired status.
/// When the Services module is implemented, this handler will dispatch a SuspendServiceCommand
/// for the linked hosting service.
/// </summary>
public sealed class DomainExpiredHandler(ILogger<DomainExpiredHandler> logger)
{
    /// <summary>
    /// Handles the domain expired event.
    /// </summary>
    /// <param name="evt">The domain expired event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task HandleAsync(DomainExpiredEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Domain {DomainId} (client {ClientId}) has expired.",
            evt.DomainId,
            evt.ClientId);

        if (evt.LinkedServiceId.HasValue)
        {
            // TODO: dispatch SuspendServiceCommand when Services module adds it
            logger.LogInformation(
                "Domain {DomainId} has linked service {ServiceId} — suspension pending Services module implementation.",
                evt.DomainId,
                evt.LinkedServiceId.Value);
        }

        return Task.CompletedTask;
    }
}
