using System.Text;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Migration.Common;

/// <summary>A migration job as the admin migration screen shows it: where it pulls from, how far it got, and whether the source is reachable.</summary>
/// <param name="Id">The job, used in every migration route.</param>
/// <param name="Key">The shared key the legacy source plugin must send back; shown once so the operator can paste it into the plugin.</param>
/// <param name="SourceUrl">The address of the legacy source the job pulls from.</param>
/// <param name="Status">Where the job is in its life: pending, in progress, completed or failed.</param>
/// <param name="Label">The operator's own name for the job; null when none was given.</param>
/// <param name="Failure">Why the job failed, as a closed reason the screen translates; null unless the job failed.</param>
/// <param name="EntitySelection">Which kinds of record the operator chose to pull.</param>
/// <param name="Progress">Imported, skipped and total counts per kind of record.</param>
/// <param name="OverallPercent">How much of the selected work is done, 0 to 100.</param>
/// <param name="PluginConnected">Whether the source plugin answered a ping in the last five minutes.</param>
/// <param name="LastPingAt">When the source plugin last answered (UTC); null when it never has.</param>
/// <param name="CreatedAt">When the job was created (UTC).</param>
/// <param name="CompletedAt">When the pull finished (UTC); null while it has not.</param>
public sealed record MigrationJobDto(
    int Id,
    [property: SecretMember] string Key,
    string SourceUrl,
    MigrationJobStatus Status,
    string? Label,
    MigrationJobFailure? Failure,
    MigrationEntitySelectionDto EntitySelection,
    MigrationProgressDto Progress,
    int OverallPercent,
    bool PluginConnected,
    DateTimeOffset? LastPingAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
