namespace Innovayse.Domain.Migration;

using Innovayse.Domain.Common;

/// <summary>
/// Aggregate root for a migration job.
/// Created by admin; key is shared with the migration plugin for authentication.
/// Tracks per-entity-type import progress and entity selection.
/// </summary>
public sealed class MigrationJob : AggregateRoot
{
    /// <summary>Secret key used by the migration plugin to authenticate requests.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>URL of the source migration plugin API endpoint.</summary>
    public string SourceUrl { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    public MigrationJobStatus Status { get; private set; } = MigrationJobStatus.Pending;

    /// <summary>Optional label set by admin (e.g. "Production migration").</summary>
    public string? Label { get; private set; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="MigrationJobStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; private set; }

    // ── Entity selection ─────────────────────────────────────────────────────

    /// <summary>Whether clients should be exported.</summary>
    public bool ExportClients { get; private set; } = true;

    /// <summary>Whether invoices should be exported.</summary>
    public bool ExportInvoices { get; private set; } = true;

    /// <summary>Whether services should be exported.</summary>
    public bool ExportServices { get; private set; } = true;

    /// <summary>Whether domains should be exported.</summary>
    public bool ExportDomains { get; private set; } = true;

    /// <summary>Whether support tickets should be exported.</summary>
    public bool ExportTickets { get; private set; } = true;

    /// <summary>Whether products should be exported.</summary>
    public bool ExportProducts { get; private set; } = true;

    /// <summary>Whether orders should be exported.</summary>
    public bool ExportOrders { get; private set; } = true;

    /// <summary>Whether transactions should be exported.</summary>
    public bool ExportTransactions { get; private set; } = true;

    /// <summary>Whether quotes should be exported.</summary>
    public bool ExportQuotes { get; private set; } = true;

    /// <summary>Whether knowledgebase articles should be exported.</summary>
    public bool ExportKnowledgebase { get; private set; } = true;

    /// <summary>Whether client contacts should be exported.</summary>
    public bool ExportContacts { get; private set; } = true;

    /// <summary>Whether ticket replies should be exported.</summary>
    public bool ExportTicketReplies { get; private set; } = true;

    /// <summary>Whether announcements should be exported.</summary>
    public bool ExportAnnouncements { get; private set; } = true;

    /// <summary>Whether downloadable files should be exported.</summary>
    public bool ExportDownloads { get; private set; } = true;

    /// <summary>Whether network issues should be exported.</summary>
    public bool ExportNetworkIssues { get; private set; } = true;

    // ── Connection ───────────────────────────────────────────────────────────

    /// <summary>Last time a successful connection test was performed.</summary>
    public DateTimeOffset? LastPingAt { get; private set; }

    // ── Progress counters ────────────────────────────────────────────────────

    /// <summary>Total number of clients to import.</summary>
    public int ClientsTotal { get; private set; }
    /// <summary>Number of clients successfully imported (new records created).</summary>
    public int ClientsImported { get; private set; }
    /// <summary>Number of clients skipped (already existed — deduplication).</summary>
    public int ClientsSkipped { get; private set; }

    /// <summary>Total number of invoices to import.</summary>
    public int InvoicesTotal { get; private set; }
    /// <summary>Number of invoices successfully imported.</summary>
    public int InvoicesImported { get; private set; }
    /// <summary>Number of invoices skipped (already existed).</summary>
    public int InvoicesSkipped { get; private set; }

    /// <summary>Total number of services to import.</summary>
    public int ServicesTotal { get; private set; }
    /// <summary>Number of services successfully imported.</summary>
    public int ServicesImported { get; private set; }
    /// <summary>Number of services skipped (already existed).</summary>
    public int ServicesSkipped { get; private set; }

    /// <summary>Total number of domains to import.</summary>
    public int DomainsTotal { get; private set; }
    /// <summary>Number of domains successfully imported.</summary>
    public int DomainsImported { get; private set; }
    /// <summary>Number of domains skipped (already existed).</summary>
    public int DomainsSkipped { get; private set; }

    /// <summary>Total number of tickets to import.</summary>
    public int TicketsTotal { get; private set; }
    /// <summary>Number of tickets successfully imported.</summary>
    public int TicketsImported { get; private set; }
    /// <summary>Number of tickets skipped (already existed).</summary>
    public int TicketsSkipped { get; private set; }

    /// <summary>Total number of products to import.</summary>
    public int ProductsTotal { get; private set; }
    /// <summary>Number of products successfully imported.</summary>
    public int ProductsImported { get; private set; }
    /// <summary>Number of products skipped (already existed).</summary>
    public int ProductsSkipped { get; private set; }

    /// <summary>Total number of orders to import.</summary>
    public int OrdersTotal { get; private set; }
    /// <summary>Number of orders successfully imported.</summary>
    public int OrdersImported { get; private set; }
    /// <summary>Number of orders skipped (already existed).</summary>
    public int OrdersSkipped { get; private set; }

    /// <summary>Total number of transactions to import.</summary>
    public int TransactionsTotal { get; private set; }
    /// <summary>Number of transactions successfully imported.</summary>
    public int TransactionsImported { get; private set; }
    /// <summary>Number of transactions skipped (already existed).</summary>
    public int TransactionsSkipped { get; private set; }

    /// <summary>Total number of quotes to import.</summary>
    public int QuotesTotal { get; private set; }
    /// <summary>Number of quotes successfully imported.</summary>
    public int QuotesImported { get; private set; }
    /// <summary>Number of quotes skipped (already existed).</summary>
    public int QuotesSkipped { get; private set; }

    /// <summary>Total number of knowledgebase articles to import.</summary>
    public int KnowledgebaseTotal { get; private set; }
    /// <summary>Number of knowledgebase articles successfully imported.</summary>
    public int KnowledgebaseImported { get; private set; }
    /// <summary>Number of knowledgebase articles skipped (already existed).</summary>
    public int KnowledgebaseSkipped { get; private set; }

    /// <summary>Total number of contacts to import.</summary>
    public int ContactsTotal { get; private set; }
    /// <summary>Number of contacts successfully imported.</summary>
    public int ContactsImported { get; private set; }
    /// <summary>Number of contacts skipped (already existed).</summary>
    public int ContactsSkipped { get; private set; }

    /// <summary>Total number of ticket replies to import.</summary>
    public int TicketRepliesTotal { get; private set; }
    /// <summary>Number of ticket replies successfully imported.</summary>
    public int TicketRepliesImported { get; private set; }
    /// <summary>Number of ticket replies skipped.</summary>
    public int TicketRepliesSkipped { get; private set; }

    /// <summary>Total number of announcements to import.</summary>
    public int AnnouncementsTotal { get; private set; }
    /// <summary>Number of announcements successfully imported.</summary>
    public int AnnouncementsImported { get; private set; }
    /// <summary>Number of announcements skipped.</summary>
    public int AnnouncementsSkipped { get; private set; }

    /// <summary>Total number of downloads to import.</summary>
    public int DownloadsTotal { get; private set; }
    /// <summary>Number of downloads successfully imported.</summary>
    public int DownloadsImported { get; private set; }
    /// <summary>Number of downloads skipped.</summary>
    public int DownloadsSkipped { get; private set; }

    /// <summary>Total number of network issues to import.</summary>
    public int NetworkIssuesTotal { get; private set; }
    /// <summary>Number of network issues successfully imported.</summary>
    public int NetworkIssuesImported { get; private set; }
    /// <summary>Number of network issues skipped.</summary>
    public int NetworkIssuesSkipped { get; private set; }

    /// <summary>When the job was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the migration completed or failed.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private MigrationJob() : base(0) { }

    /// <summary>Creates a new pending migration job with a random secret key.</summary>
    public static MigrationJob Create(
        string? label,
        string sourceUrl,
        bool exportClients,
        bool exportInvoices,
        bool exportServices,
        bool exportDomains,
        bool exportTickets,
        bool exportProducts,
        bool exportOrders,
        bool exportTransactions,
        bool exportQuotes,
        bool exportKnowledgebase,
        bool exportContacts,
        bool exportTicketReplies,
        bool exportAnnouncements = true,
        bool exportDownloads = true,
        bool exportNetworkIssues = true)
    {
        return new MigrationJob
        {
            Key = Guid.NewGuid().ToString("N"),
            Status = MigrationJobStatus.Pending,
            Label = label,
            SourceUrl = sourceUrl.TrimEnd('/'),
            ExportClients = exportClients,
            ExportInvoices = exportInvoices,
            ExportServices = exportServices,
            ExportDomains = exportDomains,
            ExportTickets = exportTickets,
            ExportProducts = exportProducts,
            ExportOrders = exportOrders,
            ExportTransactions = exportTransactions,
            ExportQuotes = exportQuotes,
            ExportKnowledgebase = exportKnowledgebase,
            ExportContacts = exportContacts,
            ExportTicketReplies = exportTicketReplies,
            ExportAnnouncements = exportAnnouncements,
            ExportDownloads = exportDownloads,
            ExportNetworkIssues = exportNetworkIssues,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Records a successful connection test.</summary>
    public void RecordPing() => LastPingAt = DateTimeOffset.UtcNow;

    /// <summary>Marks the job as in-progress and sets totals.</summary>
    public void Start(
        int clientsTotal,
        int invoicesTotal,
        int servicesTotal,
        int domainsTotal,
        int ticketsTotal,
        int productsTotal,
        int ordersTotal,
        int transactionsTotal,
        int quotesTotal,
        int knowledgebaseTotal,
        int contactsTotal,
        int ticketRepliesTotal,
        int announcementsTotal = 0,
        int downloadsTotal = 0,
        int networkIssuesTotal = 0)
    {
        Status = MigrationJobStatus.InProgress;
        ClientsTotal = clientsTotal;
        InvoicesTotal = invoicesTotal;
        ServicesTotal = servicesTotal;
        DomainsTotal = domainsTotal;
        TicketsTotal = ticketsTotal;
        ProductsTotal = productsTotal;
        OrdersTotal = ordersTotal;
        TransactionsTotal = transactionsTotal;
        QuotesTotal = quotesTotal;
        KnowledgebaseTotal = knowledgebaseTotal;
        ContactsTotal = contactsTotal;
        TicketRepliesTotal = ticketRepliesTotal;
        AnnouncementsTotal = announcementsTotal;
        DownloadsTotal = downloadsTotal;
        NetworkIssuesTotal = networkIssuesTotal;
    }

    /// <summary>Updates the imported and skipped counts for a given entity type.</summary>
    public void UpdateProgress(MigrationEntityType entityType, int importedCount, int skippedCount)
    {
        switch (entityType)
        {
            case MigrationEntityType.Clients:
                ClientsImported = importedCount;
                ClientsSkipped = skippedCount;
                break;
            case MigrationEntityType.Invoices:
                InvoicesImported = importedCount;
                InvoicesSkipped = skippedCount;
                break;
            case MigrationEntityType.Services:
                ServicesImported = importedCount;
                ServicesSkipped = skippedCount;
                break;
            case MigrationEntityType.Domains:
                DomainsImported = importedCount;
                DomainsSkipped = skippedCount;
                break;
            case MigrationEntityType.Tickets:
                TicketsImported = importedCount;
                TicketsSkipped = skippedCount;
                break;
            case MigrationEntityType.Products:
                ProductsImported = importedCount;
                ProductsSkipped = skippedCount;
                break;
            case MigrationEntityType.Orders:
                OrdersImported = importedCount;
                OrdersSkipped = skippedCount;
                break;
            case MigrationEntityType.Transactions:
                TransactionsImported = importedCount;
                TransactionsSkipped = skippedCount;
                break;
            case MigrationEntityType.Quotes:
                QuotesImported = importedCount;
                QuotesSkipped = skippedCount;
                break;
            case MigrationEntityType.Knowledgebase:
                KnowledgebaseImported = importedCount;
                KnowledgebaseSkipped = skippedCount;
                break;
            case MigrationEntityType.Contacts:
                ContactsImported = importedCount;
                ContactsSkipped = skippedCount;
                break;
            case MigrationEntityType.TicketReplies:
                TicketRepliesImported = importedCount;
                TicketRepliesSkipped = skippedCount;
                break;
            case MigrationEntityType.Announcements:
                AnnouncementsImported = importedCount;
                AnnouncementsSkipped = skippedCount;
                break;
            case MigrationEntityType.Downloads:
                DownloadsImported = importedCount;
                DownloadsSkipped = skippedCount;
                break;
            case MigrationEntityType.NetworkIssues:
                NetworkIssuesImported = importedCount;
                NetworkIssuesSkipped = skippedCount;
                break;
        }
    }

    /// <summary>Marks the job as successfully completed.</summary>
    public void Complete()
    {
        Status = MigrationJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the job as failed with an error message.</summary>
    public void Fail(string error)
    {
        Status = MigrationJobStatus.Failed;
        ErrorMessage = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Overall progress as a percentage (0–100). Returns 100 when completed.</summary>
    public int OverallPercent()
    {
        if (Status == MigrationJobStatus.Completed)
        {
            return 100;
        }

        var total = ClientsTotal + InvoicesTotal + ServicesTotal + DomainsTotal + TicketsTotal
                     + ProductsTotal + OrdersTotal + TransactionsTotal + QuotesTotal + KnowledgebaseTotal
                     + ContactsTotal + TicketRepliesTotal + AnnouncementsTotal + DownloadsTotal + NetworkIssuesTotal;
        if (total == 0)
        {
            return 0;
        }

        var processed = ClientsImported + ClientsSkipped
                      + InvoicesImported + InvoicesSkipped
                      + ServicesImported + ServicesSkipped
                      + DomainsImported + DomainsSkipped
                      + TicketsImported + TicketsSkipped
                      + ProductsImported + ProductsSkipped
                      + OrdersImported + OrdersSkipped
                      + TransactionsImported + TransactionsSkipped
                      + QuotesImported + QuotesSkipped
                      + KnowledgebaseImported + KnowledgebaseSkipped
                      + ContactsImported + ContactsSkipped
                      + TicketRepliesImported + TicketRepliesSkipped
                      + AnnouncementsImported + AnnouncementsSkipped
                      + DownloadsImported + DownloadsSkipped
                      + NetworkIssuesImported + NetworkIssuesSkipped;
        return (int)Math.Round(processed * 100.0 / total);
    }
}
