namespace Innovayse.Application.Domains.Commands.SyncPendingDomainStatuses;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Polls the registrar for every domain currently in <see cref="DomainStatus.PendingRegistration"/>
/// or <see cref="DomainStatus.PendingTransfer"/> status and activates it if the registrar reports
/// the domain as live.
/// </summary>
/// <remarks>
/// This is needed for registrars like Name.am where cart purchases are processed asynchronously
/// and the domain is not immediately active after the API call.
/// </remarks>
public sealed class SyncPendingDomainStatusesHandler(
    IDomainRepository repo,
    IRegistrarProvider registrar,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<SyncPendingDomainStatusesHandler> logger)
{
    /// <summary>Interval in minutes between consecutive sync runs.</summary>
    private const int IntervalMinutes = 15;

    /// <summary>
    /// Handles <see cref="SyncPendingDomainStatusesCommand"/>.
    /// Iterates all pending domains, polls the registrar, and activates any that are now live.
    /// Individual domain failures are logged and swallowed so one error does not abort the batch.
    /// Re-schedules itself for the next run.
    /// </summary>
    /// <param name="cmd">The sync pending domain statuses command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(SyncPendingDomainStatusesCommand cmd, CancellationToken ct)
    {
        var pending = await repo.ListPendingAsync(ct);

        if (pending.Count == 0)
        {
            logger.LogDebug("SyncPendingDomainStatuses: no pending domains found.");
        }
        else
        {
            logger.LogInformation(
                "SyncPendingDomainStatuses: checking {Count} pending domain(s).", pending.Count);

            foreach (var domain in pending)
            {
                try
                {
                    await SyncDomainAsync(domain, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "SyncPendingDomainStatuses: failed to sync domain {DomainName} (ID {DomainId}).",
                        domain.Name, domain.Id);
                }
            }
        }

        // Re-schedule the next run.
        var next = DateTimeOffset.UtcNow.AddMinutes(IntervalMinutes);
        await bus.ScheduleAsync(new SyncPendingDomainStatusesCommand(), next);
        logger.LogDebug("SyncPendingDomainStatuses: next run scheduled for {Next:u}.", next);
    }

    /// <summary>
    /// Polls the registrar for a single domain and activates it if the registrar reports it live.
    /// </summary>
    /// <param name="domain">The pending domain aggregate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SyncDomainAsync(Domain domain, CancellationToken ct)
    {
        var result = await registrar.CheckDomainActiveAsync(domain.Name, ct);

        if (!result.Success)
        {
            logger.LogDebug(
                "Domain {DomainName} (ID {DomainId}) not yet active: {Reason}",
                domain.Name, domain.Id, result.ErrorMessage);
            return;
        }

        var expiresAt = result.ExpiresAt ?? DateTimeOffset.UtcNow.AddYears(1);
        var registrarRef = result.RegistrarRef ?? domain.Name;

        if (domain.Status == DomainStatus.PendingRegistration)
        {
            domain.Activate(registrarRef);
            logger.LogInformation(
                "Domain {DomainName} (ID {DomainId}) activated via polling (expires {ExpiresAt:u}).",
                domain.Name, domain.Id, expiresAt);
        }
        else if (domain.Status == DomainStatus.PendingTransfer)
        {
            domain.ActivateTransfer(registrarRef, expiresAt);
            logger.LogInformation(
                "Domain {DomainName} (ID {DomainId}) transfer confirmed via polling (expires {ExpiresAt:u}).",
                domain.Name, domain.Id, expiresAt);
        }

        await uow.SaveChangesAsync(ct);
    }
}
