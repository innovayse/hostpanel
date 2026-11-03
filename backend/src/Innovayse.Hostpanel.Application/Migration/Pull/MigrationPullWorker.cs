using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Migration.Pull;

/// <summary>
/// Orchestrates the full pull for a migration job: fetches the source plugin's totals, then runs
/// one <see cref="IMigrationEntityImporter"/> per entity kind the job selected, in the order
/// <see cref="MigrationImportOrder"/> names (clients first, since invoices, services, domains
/// and tickets all reference them). Runs from the <c>RunMigrationPullCommand</c> handler, on the
/// background message queue.
/// </summary>
/// <remarks>
/// Each entity kind has its own importer, behind <see cref="IMigrationSource"/>. This class
/// keeps only the parts every importer shares: fetching totals, the dependency order, and the
/// failure path that records why the job stopped as a <see cref="MigrationJobFailure"/>. The
/// exception itself is logged here only; its text never reaches the job or the admin.
/// </remarks>
public sealed class MigrationPullWorker
{
    /// <summary>The migration source plugin, for the totals fetched before the first page.</summary>
    private readonly IMigrationSource _source;

    /// <summary>The module's data context, for loading and failing the job itself.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Discards the domain events imported records raise, so a pull never e-mails migrated clients.</summary>
    private readonly IDomainEventGate _eventGate;

    /// <summary>The importers in the order a pull runs them.</summary>
    private readonly IReadOnlyList<IMigrationEntityImporter> _importers;

    /// <summary>Logs the job's completion and any unhandled failure.</summary>
    private readonly ILogger<MigrationPullWorker> _logger;

    /// <summary>Stamps when the job completed or failed.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="MigrationPullWorker"/> class.</summary>
    /// <param name="source">The migration source plugin, for the totals fetched before the first page.</param>
    /// <param name="context">The module's data context, for loading and failing the job itself.</param>
    /// <param name="eventGate">Discards the domain events imported records raise, so a pull never e-mails migrated clients.</param>
    /// <param name="importers">Every entity importer; the worker runs them in <see cref="MigrationImportOrder"/> order.</param>
    /// <param name="logger">Logs the job's completion and any unhandled failure.</param>
    /// <param name="clock">Stamps when the job completed or failed.</param>
    public MigrationPullWorker(
        IMigrationSource source,
        IHostpanelDbContext context,
        IDomainEventGate eventGate,
        IEnumerable<IMigrationEntityImporter> importers,
        ILogger<MigrationPullWorker> logger,
        IClock clock)
    {
        _source = source;
        _context = context;
        _eventGate = eventGate;
        _importers = MigrationImportOrder.Arrange(importers);
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Executes the full pull for a job identified by <paramref name="jobId"/>.</summary>
    /// <param name="jobId">The migration job's own id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the job has finished, succeeded or failed.</returns>
    public async Task RunAsync(int jobId, CancellationToken ct)
    {
        // Imported clients, invoices and tickets already exist in the source system: publishing their
        // creation and payment events would send every migrated client a welcome and a receipt.
        using var suppression = _eventGate.Suppress();

        try
        {
            await PullAsync(jobId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration pull failed for job {JobId}", jobId);
            try
            {
                // Clear any pending/failed EF change tracker state before saving failure. The failure
                // is saved without the pull's token, so a cancelled pull is still recorded as such.
                _context.DetachAll();
                var job = await _context.MigrationJobs.FirstOrDefaultAsync(j => j.Id == jobId, CancellationToken.None);
                // Only a job this pull still owns is failed: one already failed by the startup
                // recovery, or never queued, is left as it is.
                if (job is { IsActive: true })
                {
                    job.Fail(Classify(ex, ct), _clock.UtcNow);
                    await _context.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save failure state for job {JobId}", jobId);
            }
        }
    }

    /// <summary>Names the reason a pull stopped from the exception it stopped with.</summary>
    /// <param name="ex">The exception the pull threw.</param>
    /// <param name="ct">The pull's cancellation token, to tell a requested stop from a timeout.</param>
    /// <returns>The failure to record against the job.</returns>
    private static MigrationJobFailure Classify(Exception ex, CancellationToken ct)
    {
        if (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            return MigrationJobFailure.Cancelled;
        }

        return ex switch
        {
            MigrationSourceAddressRefusedException => MigrationJobFailure.SourceAddressNotAllowed,
            HttpRequestException { StatusCode: not null } => MigrationJobFailure.SourceRefused,
            HttpRequestException => MigrationJobFailure.SourceUnreachable,
            TimeoutException or OperationCanceledException => MigrationJobFailure.SourceUnreachable,
            _ => MigrationJobFailure.ImportFailed,
        };
    }

    /// <summary>
    /// Fetches the source plugin's totals, starts the job, runs every selected entity kind's
    /// importer in dependency order, then marks the job complete.
    /// </summary>
    /// <param name="jobId">The migration job's own id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every selected entity kind has been imported.</returns>
    private async Task PullAsync(int jobId, CancellationToken ct)
    {
        var job = await _context.MigrationJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new InvalidOperationException($"Migration job {jobId} not found.");

        // A pull runs only for the job's queued request. A second delivery of the same message, or
        // one for a job the startup recovery already failed, finds it in another state and stops.
        if (job.Status != MigrationJobStatus.Queued)
        {
            _logger.LogInformation("Migration job {JobId} is {Status}, not queued; nothing to pull.", jobId, job.Status);
            return;
        }

        // 1. Fetch totals from plugin
        var totalsResp = await _source.GetTotalsAsync(new MigrationSourceEndpoint(job.SourceUrl, job.Key), ct);
        job.Start(new MigrationTotals(
            job.ExportClients ? totalsResp.Accounts.Clients : 0,
            job.ExportInvoices ? totalsResp.Billing.Invoices : 0,
            job.ExportServices ? totalsResp.Hosting.Services : 0,
            job.ExportDomains ? totalsResp.Hosting.Domains : 0,
            job.ExportTickets ? totalsResp.Support.Tickets : 0,
            job.ExportProducts ? totalsResp.Hosting.Products : 0,
            job.ExportOrders ? totalsResp.Billing.Orders : 0,
            job.ExportTransactions ? totalsResp.Billing.Transactions : 0,
            job.ExportQuotes ? totalsResp.Billing.Quotes : 0,
            job.ExportKnowledgebase ? totalsResp.Content.Knowledgebase : 0,
            job.ExportContacts ? totalsResp.Accounts.Contacts : 0,
            job.ExportTicketReplies ? totalsResp.Support.TicketReplies : 0,
            job.ExportAnnouncements ? totalsResp.Content.Announcements : 0,
            job.ExportDownloads ? totalsResp.Content.Downloads : 0,
            job.ExportNetworkIssues ? totalsResp.Support.NetworkIssues : 0));
        job.RecordHeartbeat(_clock.UtcNow);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Two deliveries of one pull both read the job as queued; the job row's xmin token lets only
            // one start it. The other must leave the job alone: falling through to the failure path would
            // fail a job the winner is running.
            _context.DetachAll();
            _logger.LogInformation("Migration job {JobId} was started by another pull; this one stops.", jobId);
            return;
        }

        // 2. Run each selected entity kind's importer in the order MigrationImportOrder names.
        foreach (var importer in _importers)
        {
            if (job.Includes(importer.EntityType))
            {
                await importer.ImportAsync(job, ct);
            }
        }

        job.Complete(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Migration job {JobId} completed successfully.", jobId);
    }
}
