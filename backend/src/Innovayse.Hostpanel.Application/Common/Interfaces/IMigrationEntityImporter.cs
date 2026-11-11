using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// One imported entity type's page-by-page pull from the migration source plugin into the
/// hostpanel database. <see cref="Migration.Pull.MigrationPullWorker"/> is the orchestrator that runs one of
/// these per entity kind a job selected, in the order <see cref="Migration.Pull.MigrationImportOrder"/> names.
/// </summary>
/// <remarks>
/// A documented exception to "a module owns its tables": the importers, together with
/// <c>MigrationClientResolver</c> and <c>MigrationProductLookup</c> beside them in
/// <c>Application/Migration/Importers</c>, write the tables of many modules directly. This is a bulk
/// import of legacy data across modules; it runs as one job with its own log, so routing every
/// record through each owning module's commands would split one import into many unrelated units of
/// work. The exception is listed as a writer in the architecture test's allow-list
/// (<c>CrossModuleReadModels</c>) and in <c>rules/project.md</c>.
/// </remarks>
public interface IMigrationEntityImporter
{
    /// <summary>The kind of record this importer pulls; the worker runs it only when the job selected that kind.</summary>
    public MigrationEntityType EntityType { get; }

    /// <summary>
    /// Pulls every page of this importer's entity kind for <paramref name="job"/>, imports each
    /// record, writes one <see cref="Domain.Migration.MigrationLog"/> row per record, and updates
    /// the job's own progress counters for this entity kind after each page.
    /// </summary>
    /// <param name="job">The migration job being run, already started with its totals recorded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every page for this entity kind has been imported.</returns>
    public Task ImportAsync(MigrationJob job, CancellationToken ct);
}
