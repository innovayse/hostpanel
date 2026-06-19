namespace Innovayse.Application.Domains.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainTransferredInEvent"/> and dispatches a domain-transferred notification email
/// to the owning client.
/// </summary>
public sealed class DomainTransferredNotificationHandler(
    IMessageBus bus,
    IDomainRepository domainRepo,
    IClientRepository clientRepo,
    IUserService userService,
    ILogger<DomainTransferredNotificationHandler> logger)
{
    /// <summary>
    /// Resolves the client email and sends a domain-transferred notification email.
    /// </summary>
    /// <param name="evt">The domain transferred-in domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainTransferredInEvent evt, CancellationToken ct)
    {
        try
        {
            var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
            if (client is null)
            {
                logger.LogWarning(
                    "Client {ClientId} not found for domain transferred notification (domain {DomainId}).",
                    evt.ClientId,
                    evt.DomainId);
                return;
            }

            var user = await userService.FindByIdAsync(client.UserId, ct);
            if (user is null)
            {
                logger.LogWarning(
                    "User {UserId} not found for domain transferred notification (domain {DomainId}).",
                    client.UserId,
                    evt.DomainId);
                return;
            }

            var domain = await domainRepo.FindByIdAsync(evt.DomainId, ct);

            var data = new
            {
                domain = new
                {
                    id = evt.DomainId,
                    name = evt.Name,
                    registrar = domain?.Registrar,
                    expiresAt = domain?.ExpiresAt.ToString("yyyy-MM-dd"),
                }
            };

            await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "domain-transferred", data), ct);

            logger.LogInformation(
                "Sent domain-transferred notification for domain {DomainName} (ID {DomainId}) to {Email}.",
                evt.Name,
                evt.DomainId,
                user.Value.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send domain-transferred notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
