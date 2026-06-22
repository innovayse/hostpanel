namespace Innovayse.Application.Migration.Commands.ImportBatch;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>
/// Handles one batch sent by the migration plugin.
/// Authenticates via key, updates progress counters, and completes the job on the last batch.
/// </summary>
public sealed class ImportBatchHandler(IMigrationJobRepository repo)
{
    /// <summary>Processes a single import batch and returns the updated migration job state.</summary>
    public async Task<MigrationJobDto> HandleAsync(ImportBatchCommand cmd, CancellationToken ct)
    {
        var job = await repo.GetByKeyAsync(cmd.Key, ct)
            ?? throw new InvalidOperationException("Invalid migration key.");

        if (job.Status == MigrationJobStatus.Failed || job.Status == MigrationJobStatus.Completed)
        {
            throw new InvalidOperationException($"Migration job is already {job.Status}.");
        }

        // First batch: set totals and transition to InProgress
        if (job.Status == MigrationJobStatus.Pending && cmd.Totals is not null)
        {
            job.Start(
                cmd.Totals.Clients,
                cmd.Totals.Invoices,
                cmd.Totals.Services,
                cmd.Totals.Domains,
                cmd.Totals.Tickets,
                cmd.Totals.Products,
                cmd.Totals.Orders,
                cmd.Totals.Transactions,
                cmd.Totals.Quotes,
                cmd.Totals.Knowledgebase,
                cmd.Totals.Contacts,
                cmd.Totals.TicketReplies);
        }

        var batchCount = cmd.EntityType switch
        {
            MigrationEntityType.Clients => cmd.Clients?.Count ?? 0,
            MigrationEntityType.Invoices => cmd.Invoices?.Count ?? 0,
            MigrationEntityType.Services => cmd.Services?.Count ?? 0,
            MigrationEntityType.Domains => cmd.Domains?.Count ?? 0,
            MigrationEntityType.Tickets => cmd.Tickets?.Count ?? 0,
            _ => 0,
        };

        var currentImported = cmd.EntityType switch
        {
            MigrationEntityType.Clients => job.ClientsImported,
            MigrationEntityType.Invoices => job.InvoicesImported,
            MigrationEntityType.Services => job.ServicesImported,
            MigrationEntityType.Domains => job.DomainsImported,
            MigrationEntityType.Tickets => job.TicketsImported,
            _ => 0,
        };

        job.UpdateProgress(cmd.EntityType, currentImported + batchCount, 0);

        // Complete when last batch of last entity type arrives
        if (cmd.Page >= cmd.TotalPages && cmd.EntityType == MigrationEntityType.Tickets)
        {
            job.Complete();
        }

        await repo.SaveAsync(ct);
        return job.ToDto();
    }
}
