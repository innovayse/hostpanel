namespace Innovayse.Application.Domains.Events;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="DomainExpiringEvent"/> and dispatches a domain-expiring notification email
/// to the owning client.
/// </summary>
public sealed class DomainExpiringNotificationHandler(
    IMessageBus bus,
    IClientRepository clientRepo,
    IUserService userService,
    ILogger<DomainExpiringNotificationHandler> logger)
{
    /// <summary>
    /// Resolves the client email and sends a domain-expiring notification email.
    /// </summary>
    /// <param name="evt">The domain expiring domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(DomainExpiringEvent evt, CancellationToken ct)
    {
        try
        {
            var client = await clientRepo.FindByIdAsync(evt.ClientId, ct);
            if (client is null)
            {
                logger.LogWarning(
                    "Client {ClientId} not found for domain expiring notification (domain {DomainId}).",
                    evt.ClientId,
                    evt.DomainId);
                return;
            }

            var user = await userService.FindByIdAsync(client.UserId, ct);
            if (user is null)
            {
                logger.LogWarning(
                    "User {UserId} not found for domain expiring notification (domain {DomainId}).",
                    client.UserId,
                    evt.DomainId);
                return;
            }

            var daysUntilExpiry = (int)Math.Ceiling((evt.ExpiresAt - DateTimeOffset.UtcNow).TotalDays);

            var data = new
            {
                domain = new
                {
                    id = evt.DomainId,
                    name = evt.Name,
                    expiresAt = evt.ExpiresAt.ToString("yyyy-MM-dd"),
                    daysUntilExpiry,
                }
            };

            await bus.InvokeAsync(new SendEmailCommand(user.Value.Email, "domain-expiring", data), ct);

            logger.LogInformation(
                "Sent domain-expiring notification for domain {DomainName} (ID {DomainId}, expires in {Days} days) to {Email}.",
                evt.Name,
                evt.DomainId,
                daysUntilExpiry,
                user.Value.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send domain-expiring notification for domain {DomainId}.",
                evt.DomainId);
        }
    }
}
