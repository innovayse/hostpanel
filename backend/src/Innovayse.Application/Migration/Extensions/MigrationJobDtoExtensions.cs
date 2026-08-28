namespace Innovayse.Application.Migration.Extensions;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration;

/// <summary>Extension methods for mapping <see cref="MigrationJob"/> to DTOs.</summary>
public static class MigrationJobDtoExtensions
{
    /// <summary>Maps a <see cref="MigrationJob"/> aggregate to a <see cref="MigrationJobDto"/>.</summary>
    public static MigrationJobDto ToDto(this MigrationJob j)
    {
        var completed = j.Status == MigrationJobStatus.Completed;
        return new(
            j.Id,
            j.Key,
            j.SourceUrl,
            j.Status.ToString(),
            j.Label,
            j.ErrorMessage,
            new MigrationEntitySelectionDto(
                j.ExportClients,
                j.ExportInvoices,
                j.ExportServices,
                j.ExportDomains,
                j.ExportTickets,
                j.ExportProducts,
                j.ExportOrders,
                j.ExportTransactions,
                j.ExportQuotes,
                j.ExportKnowledgebase,
                j.ExportContacts,
                j.ExportTicketReplies,
                j.ExportAnnouncements,
                j.ExportDownloads,
                j.ExportNetworkIssues),
            new MigrationProgressDto(
                new(j.ClientsImported, j.ClientsSkipped, j.ClientsTotal, completed || (j.ClientsTotal > 0 && j.ClientsImported + j.ClientsSkipped >= j.ClientsTotal)),
                new(j.InvoicesImported, j.InvoicesSkipped, j.InvoicesTotal, completed || (j.InvoicesTotal > 0 && j.InvoicesImported + j.InvoicesSkipped >= j.InvoicesTotal)),
                new(j.ServicesImported, j.ServicesSkipped, j.ServicesTotal, completed || (j.ServicesTotal > 0 && j.ServicesImported + j.ServicesSkipped >= j.ServicesTotal)),
                new(j.DomainsImported, j.DomainsSkipped, j.DomainsTotal, completed || (j.DomainsTotal > 0 && j.DomainsImported + j.DomainsSkipped >= j.DomainsTotal)),
                new(j.TicketsImported, j.TicketsSkipped, j.TicketsTotal, completed || (j.TicketsTotal > 0 && j.TicketsImported + j.TicketsSkipped >= j.TicketsTotal)),
                new(j.ProductsImported, j.ProductsSkipped, j.ProductsTotal, completed || (j.ProductsTotal > 0 && j.ProductsImported + j.ProductsSkipped >= j.ProductsTotal)),
                new(j.OrdersImported, j.OrdersSkipped, j.OrdersTotal, completed || (j.OrdersTotal > 0 && j.OrdersImported + j.OrdersSkipped >= j.OrdersTotal)),
                new(j.TransactionsImported, j.TransactionsSkipped, j.TransactionsTotal, completed || (j.TransactionsTotal > 0 && j.TransactionsImported + j.TransactionsSkipped >= j.TransactionsTotal)),
                new(j.QuotesImported, j.QuotesSkipped, j.QuotesTotal, completed || (j.QuotesTotal > 0 && j.QuotesImported + j.QuotesSkipped >= j.QuotesTotal)),
                new(j.KnowledgebaseImported, j.KnowledgebaseSkipped, j.KnowledgebaseTotal, completed || (j.KnowledgebaseTotal > 0 && j.KnowledgebaseImported + j.KnowledgebaseSkipped >= j.KnowledgebaseTotal)),
                new(j.ContactsImported, j.ContactsSkipped, j.ContactsTotal, completed || (j.ContactsTotal > 0 && j.ContactsImported + j.ContactsSkipped >= j.ContactsTotal)),
                new(j.TicketRepliesImported, j.TicketRepliesSkipped, j.TicketRepliesTotal, completed || (j.TicketRepliesTotal > 0 && j.TicketRepliesImported + j.TicketRepliesSkipped >= j.TicketRepliesTotal)),
                new(j.AnnouncementsImported, j.AnnouncementsSkipped, j.AnnouncementsTotal, completed || (j.AnnouncementsTotal > 0 && j.AnnouncementsImported + j.AnnouncementsSkipped >= j.AnnouncementsTotal)),
                new(j.DownloadsImported, j.DownloadsSkipped, j.DownloadsTotal, completed || (j.DownloadsTotal > 0 && j.DownloadsImported + j.DownloadsSkipped >= j.DownloadsTotal)),
                new(j.NetworkIssuesImported, j.NetworkIssuesSkipped, j.NetworkIssuesTotal, completed || (j.NetworkIssuesTotal > 0 && j.NetworkIssuesImported + j.NetworkIssuesSkipped >= j.NetworkIssuesTotal))),
            j.OverallPercent(),
            j.LastPingAt.HasValue && (DateTimeOffset.UtcNow - j.LastPingAt.Value).TotalMinutes < 5,
            j.LastPingAt,
            j.CreatedAt,
            j.CompletedAt);
    }
}
