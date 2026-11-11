using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Migration;

/// <summary>
/// Aggregate root for a migration job.
/// Created by admin; key is shared with the migration plugin for authentication.
/// Tracks per-entity-type import progress and entity selection.
/// </summary>
public sealed class MigrationJob : BaseEntity, IAggregateRoot
{
    /// <summary>Secret key used by the migration plugin to authenticate requests.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>URL of the source migration plugin API endpoint.</summary>
    public string SourceUrl { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    public MigrationJobStatus Status { get; private set; } = MigrationJobStatus.Pending;

    /// <summary>Optional label set by admin (e.g. "Production migration").</summary>
    public string? Label { get; private set; }

    /// <summary>Why the job failed; set only when <see cref="Status"/> is <see cref="MigrationJobStatus.Failed"/>.</summary>
    public MigrationJobFailure? Failure { get; private set; }

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

    /// <summary>
    /// The last moment a worker proved it is still running this job (UTC): set when the job is queued
    /// and refreshed while it is pulled. A queued or in-progress job whose heartbeat is old, or missing,
    /// has no worker behind it.
    /// </summary>
    public DateTimeOffset? LastHeartbeatAt { get; private set; }

    // ── Progress counters ────────────────────────────────────────────────────

    /// <summary>Total number of clients to import.</summary>
    public int ClientsTotal { get; private set; }

    /// <summary>Number of clients successfully imported (new records created).</summary>
    public int ClientsImported { get; private set; }

    /// <summary>Number of clients skipped (already existed: deduplication).</summary>
    public int ClientsSkipped { get; private set; }

    /// <summary>Number of Clients records whose import failed with an error.</summary>
    public int ClientsFailed { get; private set; }

    /// <summary>Total number of invoices to import.</summary>
    public int InvoicesTotal { get; private set; }

    /// <summary>Number of invoices successfully imported.</summary>
    public int InvoicesImported { get; private set; }

    /// <summary>Number of invoices skipped (already existed).</summary>
    public int InvoicesSkipped { get; private set; }

    /// <summary>Number of Invoices records whose import failed with an error.</summary>
    public int InvoicesFailed { get; private set; }

    /// <summary>Total number of services to import.</summary>
    public int ServicesTotal { get; private set; }

    /// <summary>Number of services successfully imported.</summary>
    public int ServicesImported { get; private set; }

    /// <summary>Number of services skipped (already existed).</summary>
    public int ServicesSkipped { get; private set; }

    /// <summary>Number of Services records whose import failed with an error.</summary>
    public int ServicesFailed { get; private set; }

    /// <summary>Total number of domains to import.</summary>
    public int DomainsTotal { get; private set; }

    /// <summary>Number of domains successfully imported.</summary>
    public int DomainsImported { get; private set; }

    /// <summary>Number of domains skipped (already existed).</summary>
    public int DomainsSkipped { get; private set; }

    /// <summary>Number of Domains records whose import failed with an error.</summary>
    public int DomainsFailed { get; private set; }

    /// <summary>Total number of tickets to import.</summary>
    public int TicketsTotal { get; private set; }

    /// <summary>Number of tickets successfully imported.</summary>
    public int TicketsImported { get; private set; }

    /// <summary>Number of tickets skipped (already existed).</summary>
    public int TicketsSkipped { get; private set; }

    /// <summary>Number of Tickets records whose import failed with an error.</summary>
    public int TicketsFailed { get; private set; }

    /// <summary>Total number of products to import.</summary>
    public int ProductsTotal { get; private set; }

    /// <summary>Number of products successfully imported.</summary>
    public int ProductsImported { get; private set; }

    /// <summary>Number of products skipped (already existed).</summary>
    public int ProductsSkipped { get; private set; }

    /// <summary>Number of Products records whose import failed with an error.</summary>
    public int ProductsFailed { get; private set; }

    /// <summary>Total number of orders to import.</summary>
    public int OrdersTotal { get; private set; }

    /// <summary>Number of orders successfully imported.</summary>
    public int OrdersImported { get; private set; }

    /// <summary>Number of orders skipped (already existed).</summary>
    public int OrdersSkipped { get; private set; }

    /// <summary>Number of Orders records whose import failed with an error.</summary>
    public int OrdersFailed { get; private set; }

    /// <summary>Total number of transactions to import.</summary>
    public int TransactionsTotal { get; private set; }

    /// <summary>Number of transactions successfully imported.</summary>
    public int TransactionsImported { get; private set; }

    /// <summary>Number of transactions skipped (already existed).</summary>
    public int TransactionsSkipped { get; private set; }

    /// <summary>Number of Transactions records whose import failed with an error.</summary>
    public int TransactionsFailed { get; private set; }

    /// <summary>Total number of quotes to import.</summary>
    public int QuotesTotal { get; private set; }

    /// <summary>Number of quotes successfully imported.</summary>
    public int QuotesImported { get; private set; }

    /// <summary>Number of quotes skipped (already existed).</summary>
    public int QuotesSkipped { get; private set; }

    /// <summary>Number of Quotes records whose import failed with an error.</summary>
    public int QuotesFailed { get; private set; }

    /// <summary>Total number of knowledgebase articles to import.</summary>
    public int KnowledgebaseTotal { get; private set; }

    /// <summary>Number of knowledgebase articles successfully imported.</summary>
    public int KnowledgebaseImported { get; private set; }

    /// <summary>Number of knowledgebase articles skipped (already existed).</summary>
    public int KnowledgebaseSkipped { get; private set; }

    /// <summary>Number of Knowledgebase records whose import failed with an error.</summary>
    public int KnowledgebaseFailed { get; private set; }

    /// <summary>Total number of contacts to import.</summary>
    public int ContactsTotal { get; private set; }

    /// <summary>Number of contacts successfully imported.</summary>
    public int ContactsImported { get; private set; }

    /// <summary>Number of contacts skipped (already existed).</summary>
    public int ContactsSkipped { get; private set; }

    /// <summary>Number of Contacts records whose import failed with an error.</summary>
    public int ContactsFailed { get; private set; }

    /// <summary>Total number of ticket replies to import.</summary>
    public int TicketRepliesTotal { get; private set; }

    /// <summary>Number of ticket replies successfully imported.</summary>
    public int TicketRepliesImported { get; private set; }

    /// <summary>Number of ticket replies skipped.</summary>
    public int TicketRepliesSkipped { get; private set; }

    /// <summary>Number of TicketReplies records whose import failed with an error.</summary>
    public int TicketRepliesFailed { get; private set; }

    /// <summary>Total number of announcements to import.</summary>
    public int AnnouncementsTotal { get; private set; }

    /// <summary>Number of announcements successfully imported.</summary>
    public int AnnouncementsImported { get; private set; }

    /// <summary>Number of announcements skipped.</summary>
    public int AnnouncementsSkipped { get; private set; }

    /// <summary>Number of Announcements records whose import failed with an error.</summary>
    public int AnnouncementsFailed { get; private set; }

    /// <summary>Total number of downloads to import.</summary>
    public int DownloadsTotal { get; private set; }

    /// <summary>Number of downloads successfully imported.</summary>
    public int DownloadsImported { get; private set; }

    /// <summary>Number of downloads skipped.</summary>
    public int DownloadsSkipped { get; private set; }

    /// <summary>Number of Downloads records whose import failed with an error.</summary>
    public int DownloadsFailed { get; private set; }

    /// <summary>Total number of network issues to import.</summary>
    public int NetworkIssuesTotal { get; private set; }

    /// <summary>Number of network issues successfully imported.</summary>
    public int NetworkIssuesImported { get; private set; }

    /// <summary>Number of network issues skipped.</summary>
    public int NetworkIssuesSkipped { get; private set; }

    /// <summary>Number of NetworkIssues records whose import failed with an error.</summary>
    public int NetworkIssuesFailed { get; private set; }

    /// <summary>When the job was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the migration completed or failed.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Required by EF Core; use <see cref="Create"/>.</summary>
    private MigrationJob() { }

    /// <summary>Creates a new pending migration job with a random secret key.</summary>
    /// <param name="setup">Label, source address and the entity types to export.</param>
    /// <returns>A new job in the pending state with a random secret key.</returns>
    public static MigrationJob Create(MigrationJobSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var selection = setup.Selection;
        return new MigrationJob
        {
            Key = Guid.NewGuid().ToString("N"),
            Status = MigrationJobStatus.Pending,
            Label = setup.Label,
            SourceUrl = setup.SourceUrl.TrimEnd('/'),
            ExportClients = selection.Clients,
            ExportInvoices = selection.Invoices,
            ExportServices = selection.Services,
            ExportDomains = selection.Domains,
            ExportTickets = selection.Tickets,
            ExportProducts = selection.Products,
            ExportOrders = selection.Orders,
            ExportTransactions = selection.Transactions,
            ExportQuotes = selection.Quotes,
            ExportKnowledgebase = selection.Knowledgebase,
            ExportContacts = selection.Contacts,
            ExportTicketReplies = selection.TicketReplies,
            ExportAnnouncements = selection.Announcements,
            ExportDownloads = selection.Downloads,
            ExportNetworkIssues = selection.NetworkIssues,
        };
    }

    /// <summary>Records a successful connection test.</summary>
    /// <param name="pingedAt">When the source answered, UTC.</param>
    public void RecordPing(DateTimeOffset pingedAt) => LastPingAt = pingedAt;

    /// <summary>
    /// Hands the job to the background queue: a pending job's first pull, or a failed job's retry.
    /// Clears the previous run's failure, completion time and counters, so a retry reports its own
    /// progress from zero, and starts the heartbeat from the moment of queuing.
    /// </summary>
    /// <param name="queuedAt">When the job was queued, UTC; the first heartbeat.</param>
    /// <exception cref="DomainRuleException">The job is not pending or failed.</exception>
    public void Queue(DateTimeOffset queuedAt)
    {
        if (Status is not (MigrationJobStatus.Pending or MigrationJobStatus.Failed))
        {
            throw InvalidTransition(nameof(Queue));
        }

        Status = MigrationJobStatus.Queued;
        Failure = null;
        CompletedAt = null;
        LastHeartbeatAt = queuedAt;
        ClientsImported = 0;
        ClientsSkipped = 0;
        ClientsFailed = 0;
        InvoicesImported = 0;
        InvoicesSkipped = 0;
        InvoicesFailed = 0;
        ServicesImported = 0;
        ServicesSkipped = 0;
        ServicesFailed = 0;
        DomainsImported = 0;
        DomainsSkipped = 0;
        DomainsFailed = 0;
        TicketsImported = 0;
        TicketsSkipped = 0;
        TicketsFailed = 0;
        ProductsImported = 0;
        ProductsSkipped = 0;
        ProductsFailed = 0;
        OrdersImported = 0;
        OrdersSkipped = 0;
        OrdersFailed = 0;
        TransactionsImported = 0;
        TransactionsSkipped = 0;
        TransactionsFailed = 0;
        QuotesImported = 0;
        QuotesSkipped = 0;
        QuotesFailed = 0;
        KnowledgebaseImported = 0;
        KnowledgebaseSkipped = 0;
        KnowledgebaseFailed = 0;
        ContactsImported = 0;
        ContactsSkipped = 0;
        ContactsFailed = 0;
        TicketRepliesImported = 0;
        TicketRepliesSkipped = 0;
        TicketRepliesFailed = 0;
        AnnouncementsImported = 0;
        AnnouncementsSkipped = 0;
        AnnouncementsFailed = 0;
        DownloadsImported = 0;
        DownloadsSkipped = 0;
        DownloadsFailed = 0;
        NetworkIssuesImported = 0;
        NetworkIssuesSkipped = 0;
        NetworkIssuesFailed = 0;
    }

    /// <summary>Records that a worker is still running the job, so recovery leaves it alone.</summary>
    /// <param name="at">The moment of the heartbeat, UTC.</param>
    /// <exception cref="DomainRuleException">The job is not queued or in progress.</exception>
    public void RecordHeartbeat(DateTimeOffset at)
    {
        if (!IsActive)
        {
            throw InvalidTransition(nameof(RecordHeartbeat));
        }

        LastHeartbeatAt = at;
    }

    /// <summary>Marks a queued job as in progress and sets its totals.</summary>
    /// <param name="totals">How many rows of each type the source reported for this job.</param>
    /// <exception cref="DomainRuleException">The job is not queued.</exception>
    public void Start(MigrationTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);

        if (Status != MigrationJobStatus.Queued)
        {
            throw InvalidTransition(nameof(Start));
        }

        Status = MigrationJobStatus.InProgress;
        ClientsTotal = totals.Clients;
        InvoicesTotal = totals.Invoices;
        ServicesTotal = totals.Services;
        DomainsTotal = totals.Domains;
        TicketsTotal = totals.Tickets;
        ProductsTotal = totals.Products;
        OrdersTotal = totals.Orders;
        TransactionsTotal = totals.Transactions;
        QuotesTotal = totals.Quotes;
        KnowledgebaseTotal = totals.Knowledgebase;
        ContactsTotal = totals.Contacts;
        TicketRepliesTotal = totals.TicketReplies;
        AnnouncementsTotal = totals.Announcements;
        DownloadsTotal = totals.Downloads;
        NetworkIssuesTotal = totals.NetworkIssues;
    }

    /// <summary>Updates the imported, skipped and failed counts for a given entity type.</summary>
    /// <param name="entityType">Which kind of record the counts are for.</param>
    /// <param name="counts">Records processed so far for that kind, by outcome.</param>
    /// <exception cref="DomainRuleException">The job is not in progress.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entityType"/> is not a defined entity type.</exception>
    public void UpdateProgress(MigrationEntityType entityType, MigrationEntityCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (Status != MigrationJobStatus.InProgress)
        {
            throw InvalidTransition(nameof(UpdateProgress));
        }

        switch (entityType)
        {
            case MigrationEntityType.Clients:
                ClientsImported = counts.Imported;
                ClientsSkipped = counts.Skipped;
                ClientsFailed = counts.Failed;
                break;
            case MigrationEntityType.Invoices:
                InvoicesImported = counts.Imported;
                InvoicesSkipped = counts.Skipped;
                InvoicesFailed = counts.Failed;
                break;
            case MigrationEntityType.Services:
                ServicesImported = counts.Imported;
                ServicesSkipped = counts.Skipped;
                ServicesFailed = counts.Failed;
                break;
            case MigrationEntityType.Domains:
                DomainsImported = counts.Imported;
                DomainsSkipped = counts.Skipped;
                DomainsFailed = counts.Failed;
                break;
            case MigrationEntityType.Tickets:
                TicketsImported = counts.Imported;
                TicketsSkipped = counts.Skipped;
                TicketsFailed = counts.Failed;
                break;
            case MigrationEntityType.Products:
                ProductsImported = counts.Imported;
                ProductsSkipped = counts.Skipped;
                ProductsFailed = counts.Failed;
                break;
            case MigrationEntityType.Orders:
                OrdersImported = counts.Imported;
                OrdersSkipped = counts.Skipped;
                OrdersFailed = counts.Failed;
                break;
            case MigrationEntityType.Transactions:
                TransactionsImported = counts.Imported;
                TransactionsSkipped = counts.Skipped;
                TransactionsFailed = counts.Failed;
                break;
            case MigrationEntityType.Quotes:
                QuotesImported = counts.Imported;
                QuotesSkipped = counts.Skipped;
                QuotesFailed = counts.Failed;
                break;
            case MigrationEntityType.Knowledgebase:
                KnowledgebaseImported = counts.Imported;
                KnowledgebaseSkipped = counts.Skipped;
                KnowledgebaseFailed = counts.Failed;
                break;
            case MigrationEntityType.Contacts:
                ContactsImported = counts.Imported;
                ContactsSkipped = counts.Skipped;
                ContactsFailed = counts.Failed;
                break;
            case MigrationEntityType.TicketReplies:
                TicketRepliesImported = counts.Imported;
                TicketRepliesSkipped = counts.Skipped;
                TicketRepliesFailed = counts.Failed;
                break;
            case MigrationEntityType.Announcements:
                AnnouncementsImported = counts.Imported;
                AnnouncementsSkipped = counts.Skipped;
                AnnouncementsFailed = counts.Failed;
                break;
            case MigrationEntityType.Downloads:
                DownloadsImported = counts.Imported;
                DownloadsSkipped = counts.Skipped;
                DownloadsFailed = counts.Failed;
                break;
            case MigrationEntityType.NetworkIssues:
                NetworkIssuesImported = counts.Imported;
                NetworkIssuesSkipped = counts.Skipped;
                NetworkIssuesFailed = counts.Failed;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown migration entity type.");
        }
    }

    /// <summary>Marks an in-progress job as successfully completed.</summary>
    /// <param name="completedAt">When the job finished, UTC.</param>
    /// <exception cref="DomainRuleException">The job is not in progress.</exception>
    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != MigrationJobStatus.InProgress)
        {
            throw InvalidTransition(nameof(Complete));
        }

        Status = MigrationJobStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Marks a queued or in-progress job as failed for the given reason. A queued job can fail
    /// before it starts, when the source cannot even report its totals or the process stops first.
    /// </summary>
    /// <param name="failure">Why the job failed.</param>
    /// <param name="failedAt">When the job failed, UTC.</param>
    /// <exception cref="DomainRuleException">The job is not queued or in progress.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failure"/> is not a defined failure.</exception>
    public void Fail(MigrationJobFailure failure, DateTimeOffset failedAt)
    {
        if (!Enum.IsDefined(failure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown migration job failure.");
        }

        if (!IsActive)
        {
            throw InvalidTransition(nameof(Fail));
        }

        Status = MigrationJobStatus.Failed;
        Failure = failure;
        CompletedAt = failedAt;
    }

    /// <summary>Whether a pull has been requested for the job and has not finished: it is queued or in progress.</summary>
    public bool IsActive => Status is MigrationJobStatus.Queued or MigrationJobStatus.InProgress;

    /// <summary>Whether the admin selected a kind of record to be pulled by this job.</summary>
    /// <param name="entityType">The kind of record.</param>
    /// <returns>True when the job pulls that kind.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entityType"/> is not a defined entity type.</exception>
    public bool Includes(MigrationEntityType entityType) => entityType switch
    {
        MigrationEntityType.Clients => ExportClients,
        MigrationEntityType.Invoices => ExportInvoices,
        MigrationEntityType.Services => ExportServices,
        MigrationEntityType.Domains => ExportDomains,
        MigrationEntityType.Tickets => ExportTickets,
        MigrationEntityType.Products => ExportProducts,
        MigrationEntityType.Orders => ExportOrders,
        MigrationEntityType.Transactions => ExportTransactions,
        MigrationEntityType.Quotes => ExportQuotes,
        MigrationEntityType.Knowledgebase => ExportKnowledgebase,
        MigrationEntityType.Contacts => ExportContacts,
        MigrationEntityType.TicketReplies => ExportTicketReplies,
        MigrationEntityType.Announcements => ExportAnnouncements,
        MigrationEntityType.Downloads => ExportDownloads,
        MigrationEntityType.NetworkIssues => ExportNetworkIssues,
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null),
    };

    /// <summary>Overall progress as a percentage (0 to 100). Returns 100 when completed.</summary>
    /// <returns>Whole percent from 0 to 100.</returns>
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

        var processed = ClientsImported + ClientsSkipped + ClientsFailed
                      + InvoicesImported + InvoicesSkipped + InvoicesFailed
                      + ServicesImported + ServicesSkipped + ServicesFailed
                      + DomainsImported + DomainsSkipped + DomainsFailed
                      + TicketsImported + TicketsSkipped + TicketsFailed
                      + ProductsImported + ProductsSkipped + ProductsFailed
                      + OrdersImported + OrdersSkipped + OrdersFailed
                      + TransactionsImported + TransactionsSkipped + TransactionsFailed
                      + QuotesImported + QuotesSkipped + QuotesFailed
                      + KnowledgebaseImported + KnowledgebaseSkipped + KnowledgebaseFailed
                      + ContactsImported + ContactsSkipped + ContactsFailed
                      + TicketRepliesImported + TicketRepliesSkipped + TicketRepliesFailed
                      + AnnouncementsImported + AnnouncementsSkipped + AnnouncementsFailed
                      + DownloadsImported + DownloadsSkipped + DownloadsFailed
                      + NetworkIssuesImported + NetworkIssuesSkipped + NetworkIssuesFailed;
        return (int)Math.Round(processed * 100.0 / total);
    }

    /// <summary>Builds the refusal for a step the job's current status does not allow.</summary>
    /// <param name="step">The method that was refused.</param>
    /// <returns>The exception to throw.</returns>
    private DomainRuleException InvalidTransition(string step) =>
        new(MigrationJobErrors.InvalidTransition, $"A migration job in status {Status} cannot {step}.");
}
