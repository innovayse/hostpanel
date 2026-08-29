namespace Innovayse.Application.Migration.Common;

/// <summary>Progress for a single entity type.</summary>
public sealed record MigrationEntityProgressDto(
    int Imported,
    int Skipped,
    int Total,
    bool Done);
