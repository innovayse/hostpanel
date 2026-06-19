namespace Innovayse.Domain.Migration;

/// <summary>Lifecycle status of a migration job.</summary>
public enum MigrationJobStatus
{
    /// <summary>Job created, waiting for the migration plugin to connect.</summary>
    Pending,

    /// <summary>Migration plugin is actively sending data.</summary>
    InProgress,

    /// <summary>All data imported successfully.</summary>
    Completed,

    /// <summary>Migration stopped due to an error.</summary>
    Failed,
}
