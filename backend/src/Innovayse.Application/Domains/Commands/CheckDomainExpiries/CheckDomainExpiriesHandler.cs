namespace Innovayse.Application.Domains.Commands.CheckDomainExpiries;

using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.MarkDomainExpired;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Events;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Scheduled handler that finds all active domains past their expiry date
/// and dispatches <see cref="MarkDomainExpiredCommand"/> for each.
/// Also sends expiry warning notifications at 30, 14, 7, and 1 days before expiry.
/// </summary>
public sealed class CheckDomainExpiriesHandler(
    IDomainRepository repo,
    IMessageBus bus,
    IUnitOfWork uow,
    ILogger<CheckDomainExpiriesHandler> logger)
{
    /// <summary>Warning intervals in days before expiry at which reminder emails are sent.</summary>
    private static readonly int[] WarningDays = [30, 14, 7, 1];

    /// <summary>
    /// Handles <see cref="CheckDomainExpiriesCommand"/>.
    /// Marks expired domains and sends expiry warning notifications for domains approaching expiry.
    /// </summary>
    /// <param name="cmd">The check domain expiries command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(CheckDomainExpiriesCommand cmd, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Mark already-expired domains ─────────────────────────────────
        var expiredDomains = await repo.ListExpiringBeforeAsync(now, ct);

        foreach (var domain in expiredDomains)
        {
            if (domain.Status == DomainStatus.Active)
            {
                await bus.InvokeAsync(new MarkDomainExpiredCommand(domain.Id), ct);
            }
        }

        // ── Send expiry warnings ─────────────────────────────────────────
        await SendExpiryWarningsAsync(now, ct);
    }

    /// <summary>
    /// Finds active domains expiring within the next 30 days and raises
    /// <see cref="DomainExpiringEvent"/> for each matching warning interval,
    /// provided a reminder of that type has not already been sent.
    /// </summary>
    /// <param name="now">The current UTC time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendExpiryWarningsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var maxThreshold = now.AddDays(WarningDays[0] + 1);
        var expiringDomains = await repo.ListExpiringBeforeAsync(maxThreshold, ct);

        foreach (var domain in expiringDomains)
        {
            if (domain.Status != DomainStatus.Active || domain.ExpiresAt <= now)
            {
                continue;
            }

            var daysUntilExpiry = (int)Math.Ceiling((domain.ExpiresAt - now).TotalDays);

            foreach (var warningDay in WarningDays)
            {
                if (daysUntilExpiry > warningDay)
                {
                    continue;
                }

                var reminderType = $"{warningDay} Days Before Expiry";

                if (domain.Reminders.Any(r => r.ReminderType == reminderType))
                {
                    continue;
                }

                try
                {
                    await bus.PublishAsync(
                        new DomainExpiringEvent(domain.Id, domain.ClientId, domain.Name, domain.ExpiresAt));

                    domain.AddReminder(reminderType, $"client-{domain.ClientId}");
                    await uow.SaveChangesAsync(ct);

                    logger.LogInformation(
                        "Raised expiry warning ({ReminderType}) for domain {DomainName} (ID {DomainId}).",
                        reminderType,
                        domain.Name,
                        domain.Id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to send expiry warning ({ReminderType}) for domain {DomainId}.",
                        reminderType,
                        domain.Id);
                }

                break;
            }
        }
    }
}
