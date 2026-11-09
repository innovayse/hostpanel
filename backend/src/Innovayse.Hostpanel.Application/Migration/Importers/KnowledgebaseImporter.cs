using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports knowledge-base categories (creating any this install does not already have, matched by
/// name), then articles deduplicated by title.
/// </summary>
public sealed class KnowledgebaseImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's knowledge-base datasets.</summary>
    private readonly MigrationRecordPager _pager;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="KnowledgebaseImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pager">Pages through the source plugin's knowledge-base datasets.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    public KnowledgebaseImporter(IHostpanelDbContext context, MigrationRecordPager pager, MigrationRecordRunner runner)
    {
        _context = context;
        _pager = pager;
        _runner = runner;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Knowledgebase;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Step 1: fetch categories, create missing ones, build sourceCatId→localCategoryName map
        var localCats = await _context.KbCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var catNameMap = new Dictionary<int, string>(); // sourceCatId → local category name

        await foreach (var page in _pager.PagesAsync<KbCategoryRecord>(job, "knowledgebase_categories", ct))
        {
            foreach (var cat in page)
            {
                var existing = localCats.FirstOrDefault(c =>
                    string.Equals(c.Name, cat.Name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    catNameMap[cat.Id] = existing.Name;
                }
                else
                {
                    var newCat = KbCategory.Create(cat.Name, cat.Description, false, null);
                    _context.KbCategories.Add(newCat);
                    await _context.SaveChangesAsync(ct);
                    catNameMap[cat.Id] = newCat.Name;
                }
            }
        }

        // Step 2: page through articles
        await _runner.RunAsync(new MigrationRecordRun<KbArticleRecord>(job, EntityType, "knowledgebase", ImportArticleAsync), ct);

        // One record of the dataset; the runner counts, logs and isolates its outcome.
        async Task<RecordImportResult> ImportArticleAsync(KbArticleRecord rec, CancellationToken ct)
        {
            var categoryName = rec.CategoryId.HasValue && catNameMap.TryGetValue(rec.CategoryId.Value, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : MigrationDefaults.KnowledgebaseCategoryName;

            var existingArticle = await _context.KbArticles.FirstOrDefaultAsync(a => a.Title.ToLower() == rec.Title.ToLower(), ct);
            if (existingArticle is not null)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }

            var article = KbArticle.Create(rec.Title, rec.Content, categoryName);

            article.Publish();

            _context.KbArticles.Add(article);
            await _context.SaveChangesAsync(ct);
            return RecordImportResult.Imported;
        }
    }
}
