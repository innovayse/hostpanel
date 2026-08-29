namespace Innovayse.Application.Migration.Common;

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
    MigrationEntityProgressDto TicketReplies,
    MigrationEntityProgressDto Announcements,
    MigrationEntityProgressDto Downloads,
    MigrationEntityProgressDto NetworkIssues);
