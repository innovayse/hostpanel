using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Constants;
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

    /// <summary>Gives each new category a slug nobody else has.</summary>
    private readonly KbSlugAllocator _slugs;

    /// <summary>Initializes a new instance of the <see cref="KnowledgebaseImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pager">Pages through the source plugin's knowledge-base datasets.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="slugs">Gives each new category and article a slug nobody else has.</param>
    public KnowledgebaseImporter(
        IHostpanelDbContext context, MigrationRecordPager pager, MigrationRecordRunner runner, KbSlugAllocator slugs)
    {
        _context = context;
        _pager = pager;
        _runner = runner;
        _slugs = slugs;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Knowledgebase;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Step 1: fetch categories, create missing ones, build sourceCatId→localCategoryId map
        var localCats = await _context.KbCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var catIdMap = new Dictionary<int, int>(); // sourceCatId → local category id

        await foreach (var page in _pager.PagesAsync<KbCategoryRecord>(job, "knowledgebase_categories", ct))
        {
            foreach (var cat in page)
            {
                var existing = localCats.FirstOrDefault(c =>
                    string.Equals(c.Name, cat.Name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    catIdMap[cat.Id] = existing.Id;
                }
                else
                {
                    var newCat = KbCategory.Create(
                        cat.Name, await _slugs.AllocateForCategoryAsync(cat.Name, ct), cat.Description, false, null);
                    _context.KbCategories.Add(newCat);
                    await _context.SaveChangesAsync(ct);
                    catIdMap[cat.Id] = newCat.Id;
                }
            }
        }

        // Step 2: page through articles
        await _runner.RunAsync(new MigrationRecordRun<KbArticleRecord>(job, EntityType, "knowledgebase", ImportArticleAsync), ct);

        // One record of the dataset; the runner counts, logs and isolates its outcome.
        async Task<RecordImportResult> ImportArticleAsync(KbArticleRecord rec, CancellationToken ct)
        {
            var title = rec.Title.ToLower();
            var exists = await _context.KbArticleTranslations.AnyAsync(t => t.Title.ToLower() == title, ct);
            if (exists)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }

            var categoryId = rec.CategoryId.HasValue && catIdMap.TryGetValue(rec.CategoryId.Value, out var mapped)
                ? mapped
                : await DefaultCategoryIdAsync(ct);

            var article = KbArticle.Create(categoryId, await _slugs.AllocateForArticleAsync(rec.Title, ct));
            // The source does not say which language an article is written in: it is stored under the platform's
            // default language, which every article must have, and the admin can add the others.
            article.SetTranslation(SupportedLanguages.Default, rec.Title, rec.Content);

            article.Publish();

            _context.KbArticles.Add(article);
            await _context.SaveChangesAsync(ct);
            return RecordImportResult.Imported;
        }
    }

    /// <summary>Finds the category that holds articles whose source category is unknown, creating it when missing.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The id of the category named <see cref="MigrationDefaults.KnowledgebaseCategoryName"/>.</returns>
    private async Task<int> DefaultCategoryIdAsync(CancellationToken ct)
    {
        var name = MigrationDefaults.KnowledgebaseCategoryName.ToLower();
        var existing = await _context.KbCategories
            .AsNoTracking()
            .Where(c => c.Name.ToLower() == name)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is { } id)
        {
            return id;
        }

        var category = KbCategory.Create(
            MigrationDefaults.KnowledgebaseCategoryName,
            await _slugs.AllocateForCategoryAsync(MigrationDefaults.KnowledgebaseCategoryName, ct),
            string.Empty,
            false);
        _context.KbCategories.Add(category);
        await _context.SaveChangesAsync(ct);

        return category.Id;
    }
}
