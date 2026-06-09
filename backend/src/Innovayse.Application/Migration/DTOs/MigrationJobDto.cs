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
    bool Tickets);

/// <summary>Per-entity-type progress breakdown.</summary>
public sealed record MigrationProgressDto(
    MigrationEntityProgressDto Clients,
    MigrationEntityProgressDto Invoices,
    MigrationEntityProgressDto Services,
    MigrationEntityProgressDto Domains,
    MigrationEntityProgressDto Tickets);

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
                j.ExportTickets),
            new MigrationProgressDto(
                new(j.ClientsImported,  j.ClientsTotal,  completed || (j.ClientsTotal  > 0 && j.ClientsImported  >= j.ClientsTotal)),
                new(j.InvoicesImported, j.InvoicesTotal, completed || (j.InvoicesTotal > 0 && j.InvoicesImported >= j.InvoicesTotal)),
                new(j.ServicesImported, j.ServicesTotal, completed || (j.ServicesTotal > 0 && j.ServicesImported >= j.ServicesTotal)),
                new(j.DomainsImported,  j.DomainsTotal,  completed || (j.DomainsTotal  > 0 && j.DomainsImported  >= j.DomainsTotal)),
                new(j.TicketsImported,  j.TicketsTotal,  completed || (j.TicketsTotal  > 0 && j.TicketsImported  >= j.TicketsTotal))),
            j.OverallPercent(),
            j.LastPingAt.HasValue && (DateTimeOffset.UtcNow - j.LastPingAt.Value).TotalMinutes < 5,
            j.LastPingAt,
            j.CreatedAt,
            j.CompletedAt);
    }
}
