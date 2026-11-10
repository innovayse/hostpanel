using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Migration.Importers.Enums;

/// <summary>
/// The outcome of importing one plugin record: created, deliberately left alone, or errored.
/// Shared by every <see cref="IMigrationEntityImporter"/> and by <see cref="MigrationRecordRunner"/>,
/// which maps it onto the persisted <see cref="Domain.Migration.Enums.MigrationLogAction"/>.
/// </summary>
public enum ImportOutcome
{
    /// <summary>The record was created here.</summary>
    Imported = 0,

    /// <summary>The record already existed, or its prerequisite was missing, so nothing was written.</summary>
    Skipped = 1,

    /// <summary>An unexpected exception was thrown while importing the record.</summary>
    Failed = 2,
}
