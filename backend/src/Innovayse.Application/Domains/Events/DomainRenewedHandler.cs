namespace Innovayse.Application.Domains.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Services.Commands.UnsuspendService;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainRenewedEvent"/> raised when a domain is successfully renewed.
/// If the domain has a linked hosting service that is currently <see cref="ServiceStatus.Suspended"/>,
/// dispatches an <see cref="UnsuspendServiceCommand"/> to reactivate it.
/// Also sends a domain-renewed notification email to the owning client.
/// </summary>
public sealed class DomainRenewedHandler(
    IDomainRepository domainRepo,
    IClientServiceRepository serviceRepo,
    IClientRepository clientRepo,
    IUserService userService,
    IMessageBus bus,
    ILogger<DomainRenewedHandler> logger)
{
    /// <summary>
    /// Handles the domain renewed event by unsuspending the linked hosting service when applicable.
    /// </summary>
    /// <param name="evt">The domain renewed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(DomainRenewedEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Domain {DomainId} (client {ClientId}) has been renewed until {NewExpiresAt}.",
            evt.DomainId,
            evt.ClientId,
            evt.NewExpiresAt);

        var domain = await domainRepo.FindByIdAsync(evt.DomainId, ct);

        if (domain?.LinkedServiceId is { } linkedServiceId)
        {
            await TryUnsuspendLinkedServiceAsync(evt.DomainId, linkedServiceId, ct);
        }

        await SendNotificationAsync(evt, domain, ct);
    }

    /// <summary>
    /// Attempts to unsuspend a linked hosting service after the domain is renewed.
    /// Only unsuspends if the service is currently in <see cref="ServiceStatus.Suspended"/> status.
    /// </summary>
    /// <param name="domainId">The renewed domain ID.</param>
    /// <param name="linkedServiceId">The linked hosting service ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous unsuspend operation.</returns>
    private async Task TryUnsuspendLinkedServiceAsync(int domainId, int linkedServiceId, CancellationToken ct)
    {
        try
        {
            var service = await serviceRepo.FindByIdAsync(linkedServiceId, ct);

            if (service is null)
            {
                logger.LogWarning(
                    "Linked service {ServiceId} for domain {DomainId} not found. Skipping unsuspend.",
                    linkedServiceId,
                    domainId);
                return;
            }

            if (service.Status != ServiceStatus.Suspended)
            {
                logger.LogInformation(
                    "Linked service {ServiceId} for domain {DomainId} is in status {Status}, not Suspended. Skipping unsuspend.",
                    linkedServiceId,
                    domainId,
                    service.Status);
                return;
            }

            await bus.InvokeAsync(new UnsuspendServiceCommand(linkedServiceId), ct);

            logger.LogInformation(
                "Unsuspended linked service {ServiceId} after domain {DomainId} renewal.",
                linkedServiceId,
                domainId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to unsuspend linked service {ServiceId} for renewed domain {DomainId}. The service may need manual reactivation.",
                linkedServiceId,
                domainId);
        }
    }

    /// <summary>
    /// Sends a domain-renewed notification email to the owning client.
    /// Failures are logged as warnings and do not propagate.
    /// </summary>
    /// <param name="evt">The domain renewed event.</param>
    /// <param name="domain">The domain entity, or <see langword="null"/> if not found.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    private async Task SendNotificationAsync(DomainRenewedEvent evt, Domain.Domains.Domain? domain, CancellationToken ct)
    {
        try
        {
            var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
            if (client is null) return;

            var user = await userService.FindByIdAsync(client.UserId, ct);
            if (user is null) return;

            var data = new
            {
                domain = new
                {
                    id = evt.DomainId,
                    name = domain?.Name,
                    newExpiresAt = evt.NewExpiresAt.ToString("yyyy-MM-dd"),
                }
            };

            await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "domain-renewed", data), ct);

            logger.LogInformation(
                "Sent domain-renewed notification for domain {DomainId} to {Email}.",
                evt.DomainId,
                user.Value.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send domain-renewed notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
