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
    /// <summary>Number of clients imported so far.</summary>
    public int ClientsImported { get; private set; }

    /// <summary>Total number of invoices to import.</summary>
    public int InvoicesTotal { get; private set; }
    /// <summary>Number of invoices imported so far.</summary>
    public int InvoicesImported { get; private set; }

    /// <summary>Total number of services to import.</summary>
    public int ServicesTotal { get; private set; }
    /// <summary>Number of services imported so far.</summary>
    public int ServicesImported { get; private set; }

    /// <summary>Total number of domains to import.</summary>
    public int DomainsTotal { get; private set; }
    /// <summary>Number of domains imported so far.</summary>
    public int DomainsImported { get; private set; }

    /// <summary>Total number of tickets to import.</summary>
    public int TicketsTotal { get; private set; }
    /// <summary>Number of tickets imported so far.</summary>
    public int TicketsImported { get; private set; }

    /// <summary>Total number of products to import.</summary>
    public int ProductsTotal { get; private set; }
    /// <summary>Number of products imported so far.</summary>
    public int ProductsImported { get; private set; }

    /// <summary>Total number of orders to import.</summary>
    public int OrdersTotal { get; private set; }
    /// <summary>Number of orders imported so far.</summary>
    public int OrdersImported { get; private set; }

    /// <summary>Total number of transactions to import.</summary>
    public int TransactionsTotal { get; private set; }
    /// <summary>Number of transactions imported so far.</summary>
    public int TransactionsImported { get; private set; }

    /// <summary>Total number of quotes to import.</summary>
    public int QuotesTotal { get; private set; }
    /// <summary>Number of quotes imported so far.</summary>
    public int QuotesImported { get; private set; }

    /// <summary>Total number of knowledgebase articles to import.</summary>
    public int KnowledgebaseTotal { get; private set; }
    /// <summary>Number of knowledgebase articles imported so far.</summary>
    public int KnowledgebaseImported { get; private set; }

    /// <summary>Total number of contacts to import.</summary>
    public int ContactsTotal { get; private set; }
    /// <summary>Number of contacts imported so far.</summary>
    public int ContactsImported { get; private set; }

    /// <summary>Total number of ticket replies to import.</summary>
    public int TicketRepliesTotal { get; private set; }
    /// <summary>Number of ticket replies imported so far.</summary>
    public int TicketRepliesImported { get; private set; }

    /// <summary>Total number of announcements to import.</summary>
    public int AnnouncementsTotal { get; private set; }
    /// <summary>Number of announcements imported so far.</summary>
    public int AnnouncementsImported { get; private set; }

    /// <summary>Total number of downloads to import.</summary>
    public int DownloadsTotal { get; private set; }
    /// <summary>Number of downloads imported so far.</summary>
    public int DownloadsImported { get; private set; }

    /// <summary>Total number of network issues to import.</summary>
    public int NetworkIssuesTotal { get; private set; }
    /// <summary>Number of network issues imported so far.</summary>
    public int NetworkIssuesImported { get; private set; }

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
            Key                  = Guid.NewGuid().ToString("N"),
            Status               = MigrationJobStatus.Pending,
            Label                = label,
            SourceUrl            = sourceUrl.TrimEnd('/'),
            ExportClients        = exportClients,
            ExportInvoices       = exportInvoices,
            ExportServices       = exportServices,
            ExportDomains        = exportDomains,
            ExportTickets        = exportTickets,
            ExportProducts       = exportProducts,
            ExportOrders         = exportOrders,
            ExportTransactions   = exportTransactions,
            ExportQuotes         = exportQuotes,
            ExportKnowledgebase  = exportKnowledgebase,
            ExportContacts       = exportContacts,
            ExportTicketReplies  = exportTicketReplies,
            ExportAnnouncements  = exportAnnouncements,
            ExportDownloads      = exportDownloads,
            ExportNetworkIssues  = exportNetworkIssues,
            CreatedAt            = DateTimeOffset.UtcNow,
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
        Status               = MigrationJobStatus.InProgress;
        ClientsTotal         = clientsTotal;
        InvoicesTotal        = invoicesTotal;
        ServicesTotal        = servicesTotal;
        DomainsTotal         = domainsTotal;
        TicketsTotal         = ticketsTotal;
        ProductsTotal        = productsTotal;
        OrdersTotal          = ordersTotal;
        TransactionsTotal    = transactionsTotal;
        QuotesTotal          = quotesTotal;
        KnowledgebaseTotal   = knowledgebaseTotal;
        ContactsTotal        = contactsTotal;
        TicketRepliesTotal   = ticketRepliesTotal;
        AnnouncementsTotal   = announcementsTotal;
        DownloadsTotal       = downloadsTotal;
        NetworkIssuesTotal   = networkIssuesTotal;
    }

    /// <summary>Updates the imported count for a given entity type.</summary>
    public void UpdateProgress(MigrationEntityType entityType, int importedCount)
    {
        switch (entityType)
        {
            case MigrationEntityType.Clients:       ClientsImported       = importedCount; break;
            case MigrationEntityType.Invoices:      InvoicesImported      = importedCount; break;
            case MigrationEntityType.Services:      ServicesImported      = importedCount; break;
            case MigrationEntityType.Domains:       DomainsImported       = importedCount; break;
            case MigrationEntityType.Tickets:       TicketsImported       = importedCount; break;
            case MigrationEntityType.Products:      ProductsImported      = importedCount; break;
            case MigrationEntityType.Orders:        OrdersImported        = importedCount; break;
            case MigrationEntityType.Transactions:  TransactionsImported  = importedCount; break;
            case MigrationEntityType.Quotes:        QuotesImported        = importedCount; break;
            case MigrationEntityType.Knowledgebase:   KnowledgebaseImported   = importedCount; break;
            case MigrationEntityType.Contacts:        ContactsImported        = importedCount; break;
            case MigrationEntityType.TicketReplies:   TicketRepliesImported   = importedCount; break;
            case MigrationEntityType.Announcements:   AnnouncementsImported   = importedCount; break;
            case MigrationEntityType.Downloads:       DownloadsImported       = importedCount; break;
            case MigrationEntityType.NetworkIssues:   NetworkIssuesImported   = importedCount; break;
        }
    }

    /// <summary>Marks the job as successfully completed.</summary>
    public void Complete()
    {
        Status      = MigrationJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the job as failed with an error message.</summary>
    public void Fail(string error)
    {
        Status       = MigrationJobStatus.Failed;
        ErrorMessage = error;
        CompletedAt  = DateTimeOffset.UtcNow;
    }

    /// <summary>Overall progress as a percentage (0–100). Returns 100 when completed.</summary>
    public int OverallPercent()
    {
        if (Status == MigrationJobStatus.Completed) return 100;
        var total    = ClientsTotal + InvoicesTotal + ServicesTotal + DomainsTotal + TicketsTotal
                     + ProductsTotal + OrdersTotal + TransactionsTotal + QuotesTotal + KnowledgebaseTotal
                     + ContactsTotal + TicketRepliesTotal + AnnouncementsTotal + DownloadsTotal + NetworkIssuesTotal;
        if (total == 0) return 0;
        var imported = ClientsImported + InvoicesImported + ServicesImported + DomainsImported + TicketsImported
                     + ProductsImported + OrdersImported + TransactionsImported + QuotesImported + KnowledgebaseImported
                     + ContactsImported + TicketRepliesImported + AnnouncementsImported + DownloadsImported + NetworkIssuesImported;
        return (int)Math.Round(imported * 100.0 / total);
    }
}
