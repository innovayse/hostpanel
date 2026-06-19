namespace Innovayse.Application.Domains.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Services.Commands.SuspendService;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainExpiredEvent"/> raised when a domain transitions to the Expired status.
/// Dispatches a <see cref="SuspendServiceCommand"/> for the linked hosting service, if any,
/// and sends a domain-expired notification email to the owning client.
/// </summary>
public sealed class DomainExpiredHandler(
    IMessageBus bus,
    IDomainRepository domainRepo,
    IClientRepository clientRepo,
    IUserService userService,
    ILogger<DomainExpiredHandler> logger)
{
    /// <summary>
    /// Handles the domain expired event by suspending the linked hosting service
    /// and sending a notification email.
    /// </summary>
    /// <param name="evt">The domain expired event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(DomainExpiredEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Domain {DomainId} (client {ClientId}) has expired.",
            evt.DomainId,
            evt.ClientId);

        if (evt.LinkedServiceId.HasValue)
        {
            try
            {
                await bus.InvokeAsync(new SuspendServiceCommand(evt.LinkedServiceId.Value), ct);

                logger.LogInformation(
                    "Suspended linked service {ServiceId} due to domain {DomainId} expiry.",
                    evt.LinkedServiceId.Value,
                    evt.DomainId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to suspend linked service {ServiceId} for expired domain {DomainId}. The service may need manual suspension.",
                    evt.LinkedServiceId.Value,
                    evt.DomainId);
            }
        }

        await SendNotificationAsync(evt, ct);
    }

    /// <summary>
    /// Sends a domain-expired notification email to the owning client.
    /// Failures are logged as warnings and do not propagate.
    /// </summary>
    /// <param name="evt">The domain expired event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    private async Task SendNotificationAsync(DomainExpiredEvent evt, CancellationToken ct)
    {
        try
        {
            var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
            if (client is null) return;

            var user = await userService.FindByIdAsync(client.UserId, ct);
            if (user is null) return;

            var domain = await domainRepo.FindByIdAsync(evt.DomainId, ct);

            var data = new
            {
                domain = new
                {
                    id = evt.DomainId,
                    name = domain?.Name,
                    linkedServiceId = evt.LinkedServiceId,
                }
            };

            await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "domain-expired", data), ct);

            logger.LogInformation(
                "Sent domain-expired notification for domain {DomainId} to {Email}.",
                evt.DomainId,
                user.Value.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send domain-expired notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
