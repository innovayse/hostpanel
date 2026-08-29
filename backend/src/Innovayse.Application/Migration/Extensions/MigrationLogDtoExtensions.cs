namespace Innovayse.Application.Migration.Extensions;

using Innovayse.Application.Migration.Common;
using Innovayse.Domain.Migration;

/// <summary>Extension methods for mapping <see cref="MigrationLog"/> to DTOs.</summary>
public static class MigrationLogDtoExtensions
{
    /// <summary>Maps a <see cref="MigrationLog"/> to a <see cref="MigrationLogDto"/>.</summary>
    public static MigrationLogDto ToDto(this MigrationLog log) =>
        new(log.Id, log.EntityType.ToString(), log.Identifier, log.Action.ToString(), log.Reason, log.CreatedAt);
}
