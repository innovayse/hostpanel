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
    bool TicketReplies,
    bool Announcements,
    bool Downloads,
    bool NetworkIssues);

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

/// <summary>Progress for a single entity type.</summary>
public sealed record MigrationEntityProgressDto(
    int Imported,
    int Skipped,
    int Total,
    bool Done);
