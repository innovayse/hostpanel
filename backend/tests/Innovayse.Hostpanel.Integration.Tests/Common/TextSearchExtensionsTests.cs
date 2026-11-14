using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// <see cref="TextSearchExtensions"/> run against the client table: the match is case-insensitive, a blank term
/// leaves the query alone, the id is only looked at by <c>SearchIncludingId</c>, and <c>%</c>, <c>_</c> and <c>\</c> match themselves.
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
