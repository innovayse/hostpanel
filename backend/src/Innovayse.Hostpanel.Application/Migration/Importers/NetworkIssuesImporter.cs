using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Enums;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports network issue records. A retried or repeated run is duplicate-safe: an issue is matched
/// by its title (case-insensitive) and start date, and one already held is skipped with
/// <see cref="MigrationSkipReason.AlreadyExists"/>. A record the source left without a start date is matched by title alone.
/// </summary>
public sealed class NetworkIssuesImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>network_issues</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Dates an issue the source record left undated.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="NetworkIssuesImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>network_issues</c> dataset and records each record's outcome.</param>
    /// <param name="clock">Dates an issue the source record left undated.</param>
    public NetworkIssuesImporter(IHostpanelDbContext context, MigrationRecordRunner runner, IClock clock)
    {
        _context = context;
        _runner = runner;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.NetworkIssues;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<NetworkIssueRecord>(job, EntityType, "network_issues", ImportNetworkIssueAsync), ct);

    /// <summary>Imports one plugin-side network issue record.</summary>
    /// <param name="rec">The network issue record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the network issue was imported, or skipped as <see cref="MigrationSkipReason.AlreadyExists"/>.</returns>
    private async Task<RecordImportResult> ImportNetworkIssueAsync(NetworkIssueRecord rec, CancellationToken ct)
    {
        var title = rec.Title.ToLower();
        var sourceStart = rec.StartDate;
        var exists = sourceStart is { } startDate
            ? await _context.NetworkIssues.AnyAsync(i => i.Title.ToLower() == title && i.StartDate == startDate, ct)
            : await _context.NetworkIssues.AnyAsync(i => i.Title.ToLower() == title, ct);
        if (exists)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // Computed once so the stored start date is the one the record was matched on.
        var start = sourceStart ?? _clock.UtcNow;

        var type = rec.Type?.ToLowerInvariant() switch
        {
            SourceNetworkIssueValues.ServerType => NetworkIssueType.Server,
            _ => NetworkIssueType.Other,
        };

        var priority = rec.Priority?.ToLowerInvariant() switch
        {
            SourceNetworkIssueValues.CriticalPriority => NetworkIssuePriority.Critical,
            SourceNetworkIssueValues.HighPriority => NetworkIssuePriority.High,
            SourceNetworkIssueValues.LowPriority => NetworkIssuePriority.Low,
            _ => NetworkIssuePriority.Medium,
        };

        var issue = NetworkIssue.Create(
            rec.Title,
            type,
            server: null,
            priority,
            start,
            string.IsNullOrWhiteSpace(rec.Description) ? "(no description)" : rec.Description);

        if (rec.Status?.ToLowerInvariant() == SourceNetworkIssueValues.Resolved)
        {
            issue.Resolve(rec.EndDate ?? _clock.UtcNow);
        }

        _context.NetworkIssues.Add(issue);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
