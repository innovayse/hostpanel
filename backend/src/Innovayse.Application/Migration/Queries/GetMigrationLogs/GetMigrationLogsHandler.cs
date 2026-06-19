namespace Innovayse.Application.Migration.Queries.GetMigrationLogs;

using Innovayse.Application.Migration.DTOs;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;

/// <summary>Handles <see cref="GetMigrationLogsQuery"/>.</summary>
public sealed class GetMigrationLogsHandler(IMigrationLogRepository logRepo)
{
    /// <summary>Returns a paginated, filtered list of migration log entries.</summary>
    public async Task<MigrationLogPageDto> HandleAsync(GetMigrationLogsQuery query, CancellationToken ct)
    {
        MigrationLogAction? action = query.Action is not null
            ? Enum.Parse<MigrationLogAction>(query.Action, ignoreCase: true)
            : null;

        MigrationEntityType? entityType = query.EntityType is not null
            ? Enum.Parse<MigrationEntityType>(query.EntityType, ignoreCase: true)
            : null;

        var (items, total) = await logRepo.ListByJobAsync(
            query.JobId, action, entityType, query.Page, query.PageSize, ct);

        return new MigrationLogPageDto(
            items.Select(l => l.ToDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }
}
