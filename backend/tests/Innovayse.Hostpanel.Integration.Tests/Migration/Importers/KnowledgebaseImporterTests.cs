using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="KnowledgebaseImporter"/>, driven end to end through
/// <c>MigrationPullWorker.RunAsync</c> against a <see cref="FakeMigrationPullSource"/>: creates a
/// missing category, then an article deduplicated by title.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class KnowledgebaseImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A category and article not already known are both created.</summary>
    [Fact]
    public async Task RunAsync_Knowledgebase_CreatesTheCategoryAndArticleAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Content = new MigrationContentTotals(Knowledgebase: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Knowledgebase);
        var marker = Guid.NewGuid().ToString("N");
        var title = $"article-{marker}";
        source.SetDatasetJson("knowledgebase_categories", $$"""
            [{"Id":1,"Name":"category-{{marker}}","Description":"d","ParentId":null}]
            """);
        source.SetDatasetJson("knowledgebase", $$"""
            [{"Title":"{{title}}","Content":"Body","CategoryId":1,"Published":true}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var article = await context.KbArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Translations.Any(t => t.Title == title));
        Assert.NotNull(article);
        Assert.True(article.IsPublished);
        Assert.Equal(title, article.Slug);
        var translation = Assert.Single(article.Translations);
        Assert.Equal(("en", "Body"), (translation.Locale, translation.Content));
        var category = await context.KbCategories.AsNoTracking().FirstAsync(c => c.Id == article.CategoryId);
        Assert.Equal($"category-{marker}", category.Name);
        Assert.Equal($"category-{marker}", category.Slug);

        // Idempotent re-import: an article matched by title is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Knowledgebase);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var articles = await context.KbArticles.AsNoTracking().Where(a => a.Translations.Any(t => t.Title == title)).ToListAsync();
        Assert.Single(articles);
    }
}
