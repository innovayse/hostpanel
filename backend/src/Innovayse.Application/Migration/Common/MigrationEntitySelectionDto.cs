namespace Innovayse.Application.Migration.Common;

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
    bool TicketReplies,
    bool Announcements,
    bool Downloads,
    bool NetworkIssues);
