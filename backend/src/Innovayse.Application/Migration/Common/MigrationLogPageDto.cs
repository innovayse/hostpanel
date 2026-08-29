namespace Innovayse.Application.Migration.Common;

/// <summary>Paged result of migration log entries.</summary>
public sealed record MigrationLogPageDto(
    IReadOnlyList<MigrationLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
