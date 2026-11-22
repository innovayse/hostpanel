using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SyncPendingDomainStatuses;

/// <summary>
/// Polls the registrar for every domain currently in <see cref="DomainStatus.PendingRegistration"/>
/// or <see cref="DomainStatus.PendingTransfer"/> status and activates it if the registrar reports
/// the domain as live.
/// </summary>
/// <remarks>
/// This is needed for registrars that process cart purchases asynchronously,
/// and the domain is not immediately active after the API call.
/// </remarks>
public sealed class SyncPendingDomainStatusesHandler
{
    /// <summary>The data context the pending domains are read from and the activation is saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SyncPendingDomainStatusesHandler> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the pending domains are read from and the activation is saved through.</param>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The source of the current moment.</param>
    public SyncPendingDomainStatusesHandler(
        IHostpanelDbContext context,
        RegistrarProviderResolver registrars,
        RecurringJobs jobs,
        ILogger<SyncPendingDomainStatusesHandler> logger,
        IClock clock)
    {
        _context = context;
        _registrars = registrars;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Handles <see cref="SyncPendingDomainStatusesCommand"/>.
    /// Iterates all pending domains, polls the registrar, and activates any that are now live.
    /// Individual domain failures are logged and swallowed so one error does not abort the batch.
    /// Re-schedules itself for the next run.
    /// </summary>
    /// <param name="cmd">The sync pending domain statuses command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task HandleAsync(SyncPendingDomainStatusesCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        return _jobs.RunAsync<SyncPendingDomainStatusesCommand>(
            _jobs.Schedules.SyncPendingDomainStatuses, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        var checkedCount = 0;
        var lastId = 0;
        while (true)
        {
            // Tracked, not AsNoTracking: each domain is activated and saved below. Keyset batches, so
            // a long backlog is never loaded at once and a domain that fails is not read again this pass.
            var pending = await _context.Domains
                .Where(x => x.Id > lastId
                            && (x.Status == DomainStatus.PendingRegistration || x.Status == DomainStatus.PendingTransfer))
                .OrderBy(x => x.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (pending.Count == 0)
            {
                break;
            }

            foreach (var domain in pending)
            {
                try
                {
                    await SyncDomainAsync(domain, ct);
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    // The failed change is still tracked and would ride along with, and fail, every later
                    // save in this loop; detaching it isolates this domain, and the next run retries it.
                    _context.Domains.Entry(domain).State = EntityState.Detached;
                    _logger.LogWarning(ex, "SyncPendingDomainStatuses: failed to sync domain {DomainId}.", domain.Id);
                }
            }

            checkedCount += pending.Count;
            lastId = pending[^1].Id;
            _context.DetachAll();
        }

        _logger.LogInformation("SyncPendingDomainStatuses: checked {Count} pending domain(s).", checkedCount);
    }

    /// <summary>
    /// Polls the registrar for a single domain and activates it if the registrar reports it live.
    /// </summary>
    /// <param name="domain">The pending domain aggregate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SyncDomainAsync(RegisteredDomain domain, CancellationToken ct)
    {
        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.CheckDomainActiveAsync(domain.ToRef(), ct);

        if (!result.Success)
        {
            _logger.LogDebug(
                "Domain {DomainId} not yet active: {Reason}",
                domain.Id, result.ErrorCode);
            return;
        }

        var now = _clock.UtcNow;
        // Without a date from the registrar, a registration keeps the expiry it was ordered with and a
        // transfer runs for the period bought with it.
        var expiresAt = result.ExpiresAt
            ?? (domain.Status == DomainStatus.PendingRegistration ? domain.ExpiresAt : now.AddYears(domain.RegistrationPeriod));
        var registrarRef = result.RegistrarRef ?? domain.Name;

        if (domain.Status == DomainStatus.PendingRegistration)
        {
            domain.Activate(registrarRef, expiresAt, now);
            _logger.LogInformation(
                "Domain {DomainId} activated via polling (expires {ExpiresAt:u}).",
                domain.Id, expiresAt);
        }
        else if (domain.Status == DomainStatus.PendingTransfer)
        {
            domain.ActivateTransfer(registrarRef, expiresAt, now);
            _logger.LogInformation(
                "Domain {DomainId} transfer confirmed via polling (expires {ExpiresAt:u}).",
                domain.Id, expiresAt);
        }

        await _context.SaveChangesAsync(ct);
    }
}
