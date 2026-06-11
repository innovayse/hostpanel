namespace Innovayse.Application.Migration.Queries.GetMigrationLogs;

/// <summary>Returns a paginated list of log entries for a migration job.</summary>
public sealed record GetMigrationLogsQuery(
    int JobId,
    string? Action,
    string? EntityType,
    int Page,
    int PageSize);
