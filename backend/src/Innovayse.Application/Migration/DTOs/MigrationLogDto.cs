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

