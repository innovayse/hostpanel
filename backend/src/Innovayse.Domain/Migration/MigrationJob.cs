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
        bool exportTickets)
    {
        return new MigrationJob
        {
            Key            = Guid.NewGuid().ToString("N"),
            Status         = MigrationJobStatus.Pending,
            Label          = label,
            SourceUrl      = sourceUrl.TrimEnd('/'),
            ExportClients  = exportClients,
            ExportInvoices = exportInvoices,
            ExportServices = exportServices,
            ExportDomains  = exportDomains,
            ExportTickets  = exportTickets,
            CreatedAt      = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Records a successful connection test.</summary>
    public void RecordPing() => LastPingAt = DateTimeOffset.UtcNow;

    /// <summary>Marks the job as in-progress and sets totals.</summary>
    public void Start(int clientsTotal, int invoicesTotal, int servicesTotal, int domainsTotal, int ticketsTotal)
    {
        Status        = MigrationJobStatus.InProgress;
        ClientsTotal  = clientsTotal;
        InvoicesTotal = invoicesTotal;
        ServicesTotal = servicesTotal;
        DomainsTotal  = domainsTotal;
        TicketsTotal  = ticketsTotal;
    }

    /// <summary>Updates the imported count for a given entity type.</summary>
    public void UpdateProgress(MigrationEntityType entityType, int importedCount)
    {
        switch (entityType)
        {
            case MigrationEntityType.Clients:  ClientsImported  = importedCount; break;
            case MigrationEntityType.Invoices: InvoicesImported = importedCount; break;
            case MigrationEntityType.Services: ServicesImported = importedCount; break;
            case MigrationEntityType.Domains:  DomainsImported  = importedCount; break;
            case MigrationEntityType.Tickets:  TicketsImported  = importedCount; break;
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
        var total    = ClientsTotal + InvoicesTotal + ServicesTotal + DomainsTotal + TicketsTotal;
        if (total == 0) return 0;
        var imported = ClientsImported + InvoicesImported + ServicesImported + DomainsImported + TicketsImported;
        return (int)Math.Round(imported * 100.0 / total);
    }
}
