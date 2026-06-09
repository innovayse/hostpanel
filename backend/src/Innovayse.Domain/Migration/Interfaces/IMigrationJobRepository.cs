namespace Innovayse.Domain.Migration.Interfaces;

/// <summary>Repository contract for <see cref="MigrationJob"/> persistence.</summary>
public interface IMigrationJobRepository
{
    /// <summary>Returns a migration job by its numeric ID, or null if not found.</summary>
    Task<MigrationJob?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns a migration job by its secret key, or null if not found.</summary>
    Task<MigrationJob?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all migration jobs ordered by creation date descending.</summary>
    Task<IReadOnlyList<MigrationJob>> ListAsync(CancellationToken ct = default);

    /// <summary>Adds a new migration job to the context.</summary>
    Task AddAsync(MigrationJob job, CancellationToken ct = default);

    /// <summary>Removes a migration job from the context.</summary>
    Task DeleteAsync(MigrationJob job, CancellationToken ct = default);

    /// <summary>Persists pending changes.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
