namespace Innovayse.Application.Migration.Common;

using Innovayse.Domain.Migration;

/// <summary>Single migration log entry returned to the admin frontend.</summary>
public sealed record MigrationLogDto(
    int Id,
    string EntityType,
    string Identifier,
    string Action,
    string? Reason,
    DateTimeOffset CreatedAt);
