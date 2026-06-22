namespace Innovayse.Application.Migration.Commands.CreateMigrationJob;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="CreateMigrationJobCommand"/>.</summary>
public sealed class CreateMigrationJobHandler(IMigrationJobRepository repo)
{
    /// <summary>Creates a new pending migration job and returns its DTO including the secret key.</summary>
    public async Task<MigrationJobDto> HandleAsync(CreateMigrationJobCommand cmd, CancellationToken ct)
    {
        const string pluginPath = "/modules/addons/innovayse_migration/api.php";
        var sourceUrl = cmd.SourceUrl.TrimEnd('/');
        if (!sourceUrl.EndsWith(pluginPath, StringComparison.OrdinalIgnoreCase))
        {
            sourceUrl += pluginPath;
        }

        var job = MigrationJob.Create(
            cmd.Label,
            sourceUrl,
            cmd.ExportClients,
            cmd.ExportInvoices,
            cmd.ExportServices,
            cmd.ExportDomains,
            cmd.ExportTickets,
            cmd.ExportProducts,
            cmd.ExportOrders,
            cmd.ExportTransactions,
            cmd.ExportQuotes,
            cmd.ExportKnowledgebase,
            cmd.ExportContacts,
            cmd.ExportTicketReplies,
            cmd.ExportAnnouncements,
            cmd.ExportDownloads,
            cmd.ExportNetworkIssues);

        await repo.AddAsync(job, ct);
        await repo.SaveAsync(ct);
        return job.ToDto();
    }
}
