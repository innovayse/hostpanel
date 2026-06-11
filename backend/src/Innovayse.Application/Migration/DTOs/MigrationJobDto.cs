namespace Innovayse.Application.Migration.DTOs;

using Innovayse.Domain.Migration;

/// <summary>Migration job summary returned to the admin frontend.</summary>
public sealed record MigrationJobDto(
    int Id,
    string Key,
    string SourceUrl,
    string Status,
    string? Label,
    string? ErrorMessage,
    MigrationEntitySelectionDto EntitySelection,
    MigrationProgressDto Progress,
    int OverallPercent,
    bool PluginConnected,
    DateTimeOffset? LastPingAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Which entity types are enabled for this migration job.</summary>
public sealed record MigrationEntitySelectionDto(
    bool Clients,
    bool Invoices,
    bool Services,
    bool Domains,
    bool Tickets,
    bool Products,
    bool Orders,
    bool Transactions,
    bool Quotes,
    bool Knowledgebase,
    bool Contacts,
    bool TicketReplies);

/// <summary>Per-entity-type progress breakdown.</summary>
public sealed record MigrationProgressDto(
    MigrationEntityProgressDto Clients,
    MigrationEntityProgressDto Invoices,
    MigrationEntityProgressDto Services,
    MigrationEntityProgressDto Domains,
    MigrationEntityProgressDto Tickets,
    MigrationEntityProgressDto Products,
    MigrationEntityProgressDto Orders,
    MigrationEntityProgressDto Transactions,
    MigrationEntityProgressDto Quotes,
    MigrationEntityProgressDto Knowledgebase,
    MigrationEntityProgressDto Contacts,
    MigrationEntityProgressDto TicketReplies);

/// <summary>Progress for a single entity type.</summary>
public sealed record MigrationEntityProgressDto(
    int Imported,
    int Total,
    bool Done);

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
                j.ExportTicketReplies),
            new MigrationProgressDto(
                new(j.ClientsImported,        j.ClientsTotal,        completed || (j.ClientsTotal        > 0 && j.ClientsImported        >= j.ClientsTotal)),
                new(j.InvoicesImported,       j.InvoicesTotal,       completed || (j.InvoicesTotal       > 0 && j.InvoicesImported       >= j.InvoicesTotal)),
                new(j.ServicesImported,       j.ServicesTotal,       completed || (j.ServicesTotal       > 0 && j.ServicesImported       >= j.ServicesTotal)),
                new(j.DomainsImported,        j.DomainsTotal,        completed || (j.DomainsTotal        > 0 && j.DomainsImported        >= j.DomainsTotal)),
                new(j.TicketsImported,        j.TicketsTotal,        completed || (j.TicketsTotal        > 0 && j.TicketsImported        >= j.TicketsTotal)),
                new(j.ProductsImported,       j.ProductsTotal,       completed || (j.ProductsTotal       > 0 && j.ProductsImported       >= j.ProductsTotal)),
                new(j.OrdersImported,         j.OrdersTotal,         completed || (j.OrdersTotal         > 0 && j.OrdersImported         >= j.OrdersTotal)),
                new(j.TransactionsImported,   j.TransactionsTotal,   completed || (j.TransactionsTotal   > 0 && j.TransactionsImported   >= j.TransactionsTotal)),
                new(j.QuotesImported,         j.QuotesTotal,         completed || (j.QuotesTotal         > 0 && j.QuotesImported         >= j.QuotesTotal)),
                new(j.KnowledgebaseImported,  j.KnowledgebaseTotal,  completed || (j.KnowledgebaseTotal  > 0 && j.KnowledgebaseImported  >= j.KnowledgebaseTotal)),
                new(j.ContactsImported,       j.ContactsTotal,       completed || (j.ContactsTotal       > 0 && j.ContactsImported       >= j.ContactsTotal)),
                new(j.TicketRepliesImported,  j.TicketRepliesTotal,  completed || (j.TicketRepliesTotal  > 0 && j.TicketRepliesImported  >= j.TicketRepliesTotal))),
            j.OverallPercent(),
            j.LastPingAt.HasValue && (DateTimeOffset.UtcNow - j.LastPingAt.Value).TotalMinutes < 5,
            j.LastPingAt,
            j.CreatedAt,
            j.CompletedAt);
    }
}
