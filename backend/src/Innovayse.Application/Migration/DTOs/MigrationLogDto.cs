namespace Innovayse.Application.Migration.DTOs;

using Innovayse.Domain.Migration;

/// <summary>Single migration log entry returned to the admin frontend.</summary>
public sealed record MigrationLogDto(
    int Id,
    string EntityType,
    string Identifier,
    string Action,
    string? Reason,
    DateTimeOffset CreatedAt);

/// <summary>Paged result of migration log entries.</summary>
public sealed record MigrationLogPageDto(
    IReadOnlyList<MigrationLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Extension methods for mapping <see cref="MigrationLog"/> to DTOs.</summary>
public static class MigrationLogDtoExtensions
{
    /// <summary>Maps a <see cref="MigrationLog"/> to a <see cref="MigrationLogDto"/>.</summary>
    public static MigrationLogDto ToDto(this MigrationLog log) =>
        new(log.Id, log.EntityType.ToString(), log.Identifier, log.Action.ToString(), log.Reason, log.CreatedAt);
}
