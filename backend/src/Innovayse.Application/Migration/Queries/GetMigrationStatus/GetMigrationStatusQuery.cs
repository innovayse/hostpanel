namespace Innovayse.Application.Migration.Queries.GetMigrationStatus;

/// <summary>Returns the current status and progress of a migration job.</summary>
public sealed record GetMigrationStatusQuery(int JobId);
