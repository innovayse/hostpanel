using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// <see cref="TextSearchExtensions"/> run against the client table and the knowledge base articles: the match is
/// case-insensitive, a blank term leaves the query alone, the id is only looked at by <c>SearchIncludingId</c>,
/// <c>%</c>, <c>_</c> and <c>\</c> match themselves, and the sub-collection overload finds a row by any of its children
/// and lists it once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TextSearchExtensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A column selector alone narrows by the text, in any letter case, over a nullable column.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_MatchesAColumnInAnyCase()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (match, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Case{token}", phone: $"+44 {token}");
        var (noPhone, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Case{token}");

        var ids = await SearchAsync(q => q.Search($"+44 {token.ToUpperInvariant()}", c => c.Phone), token);

        ids.Should().Contain(match).And.NotContain(noPhone);
    }

    /// <summary>A blank term leaves every row in place.</summary>
    /// <param name="term">The blank search text.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_WithABlankTermKeepsEveryRow(string? term)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (first, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Blank{token}");
        var (second, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Blank{token}");

        var ids = await SearchAsync(q => q.Search(term, c => c.Phone), token);

        ids.Should().Contain([first, second]);
    }

    /// <summary>Plain <c>Search</c> does not read a whole number as an id, <c>SearchIncludingId</c> does.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_DoesNotMatchAnId()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (id, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Ada", lastName: $"NoId{token}");
        var term = id.ToString(CultureInfo.InvariantCulture);

        // The searched column is the first name, which holds no digits, so only the id can make the row match.
        var withoutId = await SearchAsync(q => q.Search(term, c => c.FirstName), token);
        var withId = await SearchAsync(q => q.SearchIncludingId(term, c => c.Id, c => c.FirstName), token);

        withoutId.Should().NotContain(id);
        withId.Should().Contain(id);
    }

    /// <summary><c>%</c>, <c>_</c> and <c>\</c> typed as text match only themselves.</summary>
    /// <param name="stored">The company text a client has.</param>
    /// <param name="term">The search text that must find it, and nothing that merely fits it as a pattern.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("100% pure", "100%")]
    [InlineData("a_b", "a_b")]
    [InlineData("back\\slash", "back\\slash")]
    public async Task Search_TreatsWildcardAndEscapeCharactersAsText(string stored, string term)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var (exact, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Lit{token}", companyName: $"{token} {stored}");
        var (lookalike, _) = await ClientsTestSeeds.SeedClientAsync(factory, lastName: $"Lit{token}", companyName: $"{token} 100 pure axb backXslash");

        var ids = await SearchAsync(q => q.Search($"{token} {term}", c => c.CompanyName), token);

        ids.Should().Contain(exact).And.NotContain(lookalike);
    }

    /// <summary>The sub-collection search finds an article by text in any of its translations, not only the first one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchChildren_MatchesATextInANonFirstTranslation()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var inRussian = await SeedArticleAsync(token, ("en", "English title", "English body"), ("ru", $"Заголовок {token}", "Текст"));
        var inEnglish = await SeedArticleAsync(token, ("en", $"Title {token}", "Body"), ("ru", "Заголовок", "Текст"));
        var unrelated = await SeedArticleAsync(token, ("en", "Unrelated", "Nothing"), ("ru", "Другое", "Ничего"));

        var ids = await SearchArticlesAsync(q => q.Search(token, a => a.Translations, t => t.Title, t => t.Content), token);

        ids.Should().BeEquivalentTo([inRussian, inEnglish]).And.NotContain(unrelated);
    }

    /// <summary>The sub-collection search ignores letter case and looks in every column it is given.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchChildren_IgnoresCaseAndLooksInEveryColumn()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var byTitle = await SeedArticleAsync(token, ("en", $"Title {token}", "Body"));
        var byContent = await SeedArticleAsync(token, ("en", "Title", $"Body {token}"));

        var upper = await SearchArticlesAsync(q => q.Search(token.ToUpperInvariant(), a => a.Translations, t => t.Title, t => t.Content), token);
        var titleOnly = await SearchArticlesAsync(q => q.Search(token, a => a.Translations, t => t.Title), token);

        upper.Should().BeEquivalentTo([byTitle, byContent]);
        titleOnly.Should().Equal(byTitle);
    }

    /// <summary><c>%</c>, <c>_</c> and <c>\</c> typed as text match only themselves in the children too.</summary>
    /// <param name="stored">The title text an article has.</param>
    /// <param name="term">The search text that must find it, and nothing that merely fits it as a pattern.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("100% pure", "100%")]
    [InlineData("a_b", "a_b")]
    [InlineData("back\\slash", "back\\slash")]
    public async Task SearchChildren_TreatsWildcardAndEscapeCharactersAsText(string stored, string term)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var exact = await SeedArticleAsync(token, ("en", $"{token} {stored}", "Body"));
        var lookalike = await SeedArticleAsync(token, ("en", $"{token} 100 pure axb backXslash", "Body"));

        var ids = await SearchArticlesAsync(q => q.Search($"{token} {term}", a => a.Translations, t => t.Title), token);

        ids.Should().Contain(exact).And.NotContain(lookalike);
    }

    /// <summary>A blank term leaves every article in place.</summary>
    /// <param name="term">The blank search text.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchChildren_WithABlankTermKeepsEveryRow(string? term)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var first = await SeedArticleAsync(token, ("en", "One", "Body"));
        var second = await SeedArticleAsync(token, ("en", "Two", "Body"));

        var ids = await SearchArticlesAsync(q => q.Search(term, a => a.Translations, t => t.Title), token);

        ids.Should().BeEquivalentTo([first, second]);
    }

    /// <summary>An article whose text matches in several translations is listed once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchChildren_ListsAParentOnceWhenSeveralChildrenMatch()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var id = await SeedArticleAsync(token, ("en", $"Title {token}", $"Body {token}"), ("ru", $"Заголовок {token}", token), ("hy", token, token));

        var ids = await SearchArticlesAsync(q => q.Search(token, a => a.Translations, t => t.Title, t => t.Content), token);

        ids.Should().Equal(id);
    }

    /// <summary>Seeds an article with the given translations in a category of its own.</summary>
    /// <param name="token">Starts the article slug, so a test finds only the articles it seeded.</param>
    /// <param name="translations">The translations as (locale, title, content).</param>
    /// <returns>The new article id.</returns>
    private async Task<int> SeedArticleAsync(string token, params (string Locale, string Title, string Content)[] translations)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var category = KbCategory.Create($"Category {unique}", $"kb-{unique}", "A category.", false);
        db.KbCategories.Add(category);
        await db.SaveChangesAsync(CancellationToken.None);

        var article = KbArticle.Create(category.Id, $"kb-{token}-{unique}");
        foreach (var (locale, title, content) in translations)
        {
            article.SetTranslation(locale, title, content);
        }

        db.KbArticles.Add(article);
        await db.SaveChangesAsync(CancellationToken.None);
        return article.Id;
    }

    /// <summary>Applies a search to the articles one test seeded and returns the ids it keeps.</summary>
    /// <param name="narrow">Applies the search under test to the query.</param>
    /// <param name="token">Starts the slug of every article the test seeded, so the rows of other tests are ignored.</param>
    /// <returns>The ids of the articles the search kept.</returns>
    private async Task<List<int>> SearchArticlesAsync(Func<IQueryable<KbArticle>, IQueryable<KbArticle>> narrow, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var prefix = $"kb-{token}-";

        return await narrow(db.KbArticles.AsNoTracking().Where(a => a.Slug.StartsWith(prefix)))
            .Select(a => a.Id)
            .ToListAsync();
    }

    /// <summary>Applies a search to the clients one test seeded and returns the ids it keeps.</summary>
    /// <param name="narrow">Applies the search under test to the query.</param>
    /// <param name="token">Ends the last name of every client the test seeded, so other tests' rows are ignored.</param>
    /// <returns>The ids of the clients the search kept.</returns>
    private async Task<List<int>> SearchAsync(Func<IQueryable<Client>, IQueryable<Client>> narrow, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        return await narrow(db.Clients.AsNoTracking().Where(c => c.LastName.EndsWith(token)))
            .Select(c => c.Id)
            .ToListAsync();
    }
}
