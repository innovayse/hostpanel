using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Tests for <see cref="KbSlugSaver"/>, the one place that saves a new article or category under a slug nobody else
/// has and tries again when the unique index says another request took it first, against the real database.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class KbSlugSaverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The unique index refuses the first save, so the second attempt gets the next free slug.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SlugTakenBetweenAllocationAndSave_IsRetriedWithTheNextFreeSlugAsync()
    {
        using var scope = factory.Services.CreateScope();
        var (context, saver, categoryId) = await ArrangeAsync(scope);
        var taken = $"race-{Guid.NewGuid():N}";
        context.KbArticles.Add(KbArticle.Create(categoryId, taken));
        await context.SaveChangesAsync(CancellationToken.None);
        var allocations = new List<string>();

        // The allocator answers with the slug that was free a moment ago and is now stored, then with the next one.
        Task<string> Allocate(CancellationToken token)
        {
            allocations.Add(allocations.Count == 0 ? taken : $"{taken}-2");
            return Task.FromResult(allocations[^1]);
        }

        var article = await saver.SaveAsync(
            Allocate,
            slug => KbArticle.Create(categoryId, slug),
            a => context.KbArticles.Add(a),
            a => context.KbArticles.Remove(a),
            (slug, token) => context.KbArticles.AsNoTracking().AnyAsync(a => a.Slug == slug, token),
            CancellationToken.None);

        article.Slug.Should().Be($"{taken}-2");
        allocations.Should().Equal(taken, $"{taken}-2");
        (await context.KbArticles.AsNoTracking().CountAsync(a => a.Slug.StartsWith(taken))).Should().Be(2, "the refused entity is not left behind");
    }

    /// <summary>A slug taken on every attempt ends in the refusal after the last attempt, not in a loop.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SlugTakenOnEveryAttempt_IsRefusedAfterTheLastAttemptAsync()
    {
        using var scope = factory.Services.CreateScope();
        var (context, saver, categoryId) = await ArrangeAsync(scope);
        var taken = $"always-{Guid.NewGuid():N}";
        context.KbArticles.Add(KbArticle.Create(categoryId, taken));
        await context.SaveChangesAsync(CancellationToken.None);
        var attempts = 0;

        var act = () => saver.SaveAsync(
            token =>
            {
                attempts++;
                return Task.FromResult(taken);
            },
            slug => KbArticle.Create(categoryId, slug),
            a => context.KbArticles.Add(a),
            a => context.KbArticles.Remove(a),
            (slug, token) => context.KbArticles.AsNoTracking().AnyAsync(a => a.Slug == slug, token),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        attempts.Should().Be(KbSlugSaver.MaxAttempts);
    }

    /// <summary>A refusal that is not about the slug is rethrown at once, without trying again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusalThatIsNotTheSlug_IsRethrownAtOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var (context, saver, _) = await ArrangeAsync(scope);
        var attempts = 0;

        // No such category: the foreign key refuses the save, and the slug is not stored, so there is nothing to retry.
        var act = () => saver.SaveAsync(
            token =>
            {
                attempts++;
                return Task.FromResult($"orphan-{Guid.NewGuid():N}");
            },
            slug => KbArticle.Create(987_654_321, slug),
            a => context.KbArticles.Add(a),
            a => context.KbArticles.Remove(a),
            (slug, token) => context.KbArticles.AsNoTracking().AnyAsync(a => a.Slug == slug, token),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        attempts.Should().Be(1);
    }

    /// <summary>The convenience methods save a category and an article under slugs built from their text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SaveCategoryAndArticle_BuildTheSlugsFromTheirTextAsync()
    {
        using var scope = factory.Services.CreateScope();
        var saver = scope.ServiceProvider.GetRequiredService<KbSlugSaver>();
        var marker = Guid.NewGuid().ToString("N")[..8];

        var category = await saver.SaveCategoryAsync($"Saver Category {marker}", slug => KbCategory.Create($"Saver Category {marker}", slug, string.Empty, false), CancellationToken.None);
        var article = await saver.SaveArticleAsync($"Saver Article {marker}", slug => KbArticle.Create(category.Id, slug), CancellationToken.None);

        category.Slug.Should().Be($"saver-category-{marker}");
        article.Slug.Should().Be($"saver-article-{marker}");
    }

    /// <summary>Resolves what a test needs and saves a category to put articles in.</summary>
    /// <param name="scope">The scope the services are resolved in.</param>
    /// <returns>The data context, the saver and the id of a fresh category.</returns>
    private static async Task<(IHostpanelDbContext Context, KbSlugSaver Saver, int CategoryId)> ArrangeAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var saver = scope.ServiceProvider.GetRequiredService<KbSlugSaver>();
        var name = $"Saver {Guid.NewGuid():N}";
        var category = await saver.SaveCategoryAsync(name, slug => KbCategory.Create(name, slug, string.Empty, false), CancellationToken.None);

        return (context, saver, category.Id);
    }
}
