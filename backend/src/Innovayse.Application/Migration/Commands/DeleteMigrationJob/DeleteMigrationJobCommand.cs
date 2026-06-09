namespace Innovayse.Application.Migration.Commands.DeleteMigrationJob;

/// <summary>Deletes a migration job by ID.</summary>
/// <param name="JobId">ID of the job to delete.</param>
public sealed record DeleteMigrationJobCommand(int JobId);
