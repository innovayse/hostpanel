using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Enums;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports announcement records. A retried or repeated run is duplicate-safe: an announcement is
/// matched by its title (case-insensitive) and creation date, and one already held is skipped with
/// <see cref="MigrationSkipReason.AlreadyExists"/>. A record the source left undated is matched by title alone.
/// </summary>
public sealed class AnnouncementsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>announcements</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="AnnouncementsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>announcements</c> dataset and records each record's outcome.</param>
    public AnnouncementsImporter(IHostpanelDbContext context, MigrationRecordRunner runner)
    {
        _context = context;
        _runner = runner;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Announcements;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<AnnouncementRecord>(job, EntityType, "announcements", ImportAnnouncementAsync), ct);

    /// <summary>Imports one plugin-side announcement record.</summary>
    /// <param name="rec">The announcement record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the announcement was imported, or skipped as <see cref="MigrationSkipReason.AlreadyExists"/>.</returns>
    private async Task<RecordImportResult> ImportAnnouncementAsync(AnnouncementRecord rec, CancellationToken ct)
    {
        var title = rec.Title.ToLower();
        // Cut to the stored precision so the date compares equal to the one a first run wrote.
        var createdAt = rec.CreatedAt is { } sourceDate ? MigrationTimestamps.ToMicroseconds(sourceDate) : (DateTimeOffset?)null;
        // Lookup is served by IX_Announcements_CreatedAt_Title (the equality column leads); the title-only
        // fallback for an undated record has no usable index and is a sequential scan, acceptable for a rare case.
        var exists = createdAt is { } date
            ? await _context.Announcements.AnyAsync(a => a.CreatedAt == date && a.Title.ToLower() == title, ct)
            : await _context.Announcements.AnyAsync(a => a.Title.ToLower() == title, ct);
        if (exists)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        var announcement = Announcement.Create(rec.Title, rec.Content, rec.Published);
        if (createdAt is { } writtenAt)
        {
            announcement.Backdate(writtenAt);
        }

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
