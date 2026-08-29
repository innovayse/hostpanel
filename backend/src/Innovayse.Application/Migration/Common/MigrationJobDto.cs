namespace Innovayse.Application.Migration.Common;

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
