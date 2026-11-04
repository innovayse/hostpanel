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
/// Imports download categories (matched by name among top-level categories, created when missing),
/// then downloadable files placed into the imported category (or the first available one; a file is
/// skipped when there is no category at all). A file is matched by title (case-insensitive) and
/// category, and one already held is skipped with <see cref="MigrationSkipReason.AlreadyExists"/>.
/// </summary>
public sealed class DownloadsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's download datasets.</summary>
    private readonly MigrationRecordPager _pager;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="DownloadsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pager">Pages through the source plugin's download datasets.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    public DownloadsImporter(IHostpanelDbContext context, MigrationRecordPager pager, MigrationRecordRunner runner)
    {
        _context = context;
        _pager = pager;
        _runner = runner;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Downloads;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Step 1: categories
        var catIdMap = new Dictionary<int, int>(); // sourceCatId → localCatId

        await foreach (var page in _pager.PagesAsync<DownloadCategoryRecord>(job, "download_categories", ct))
        {
            foreach (var cat in page)
            {
                var name = cat.Name.ToLower();
                var existingId = await _context.DownloadCategories
                    .Where(c => c.ParentCategoryId == null && c.Name.ToLower() == name)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync(ct);
                if (existingId is not null)
                {
                    catIdMap[cat.Id] = existingId.Value;
                    continue;
                }

                var newCat = DownloadCategory.Create(cat.Name, cat.Description ?? string.Empty, cat.Hidden, null);
                _context.DownloadCategories.Add(newCat);
                await _context.SaveChangesAsync(ct);
                catIdMap[cat.Id] = newCat.Id;
            }
        }

        // Step 2: files
        await _runner.RunAsync(new MigrationRecordRun<DownloadRecord>(job, EntityType, "downloads", ImportDownloadAsync), ct);

        // One record of the dataset; the runner counts, logs and isolates its outcome.
        async Task<RecordImportResult> ImportDownloadAsync(DownloadRecord rec, CancellationToken ct)
        {
            int? catId = rec.CategoryId.HasValue && catIdMap.TryGetValue(rec.CategoryId.Value, out var localCatId)
                ? localCatId
                : catIdMap.Values.Select(id => (int?)id).FirstOrDefault();
            if (catId is null)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.DownloadCategoryNotFound);
            }

            var resolvedTitle = !string.IsNullOrWhiteSpace(rec.Title) ? rec.Title
                : !string.IsNullOrWhiteSpace(rec.Filename) ? rec.Filename
                : MigrationDefaults.DownloadTitle;

            var lowerTitle = resolvedTitle.ToLower();
            var localCategoryId = catId.Value;
            var exists = await _context.Downloads.AnyAsync(d => d.CategoryId == localCategoryId && d.Title.ToLower() == lowerTitle, ct);
            if (exists)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }

            var download = Download.Create(
                resolvedTitle,
                rec.Description ?? string.Empty,
                rec.Type,
                rec.Filename,
                catId.Value,
                rec.ClientsOnly,
                false,
                rec.Hidden);

            _context.Downloads.Add(download);
            await _context.SaveChangesAsync(ct);
            return RecordImportResult.Imported;
        }
    }
}
