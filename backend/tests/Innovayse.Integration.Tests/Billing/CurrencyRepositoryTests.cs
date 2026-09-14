namespace Innovayse.Integration.Tests.Billing;

using FluentAssertions;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// <see cref="ICurrencyRepository"/> against the migrated schema: the seed the
/// <c>AddCurrencies</c> migration wrote, the ordering the interface promises, and the partial
/// unique index that keeps the base currency singular.
/// </summary>
/// <remarks>
/// A real PostgreSQL rather than an in-memory provider, because two of the things worth pinning
/// only exist there — the migration's seed rows and the filtered index — and because the context's
/// full model (jsonb dictionaries on <c>TldConfig</c>) does not build on any other provider.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CurrencyRepositoryTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The migration seeded USD as the base and AMD beside it; the list is base first.</summary>
    [Fact]
    public async Task MigrationSeedsUsdAsBaseAndAmdRated()
    {
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICurrencyRepository>();

        var baseCurrency = await repo.GetBaseAsync(CancellationToken.None);
        var all = await repo.ListAsync(enabledOnly: true, CancellationToken.None);

        baseCurrency.Code.Should().Be("USD");
        baseCurrency.RateToBase.Should().Be(1m);
        all.Select(c => c.Code).Should().StartWith("USD").And.Contain("AMD");
        all.Single(c => c.Code == "AMD").RateToBase.Should().Be(0.00256410m);
        all.Single(c => c.Code == "AMD").Decimals.Should().Be(0);
    }

    /// <summary>A lookup by code is case-insensitive, as the interface promises.</summary>
    [Fact]
    public async Task FindAsyncIgnoresCase()
    {
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICurrencyRepository>();

        var found = await repo.FindAsync("amd", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Code.Should().Be("AMD");
    }

    /// <summary>A currency added through the repository is read back by a fresh context.</summary>
    [Fact]
    public async Task AddPersistsARatedCurrency()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICurrencyRepository>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            repo.Add(Currency.Create("GBP", "826", "£", string.Empty, 2, 1.27m, isBase: false));
            await db.SaveChangesAsync();
        }

        using var read = factory.Services.CreateScope();
        var reader = read.ServiceProvider.GetRequiredService<ICurrencyRepository>();
        var gbp = await reader.FindAsync("GBP", CancellationToken.None);

        gbp.Should().NotBeNull();
        gbp!.Prefix.Should().Be("£");
        gbp.RateToBase.Should().Be(1.27m);
    }

    /// <summary>The partial unique index refuses a second base row at the database, not only in code.</summary>
    [Fact]
    public async Task ASecondBaseIsRefusedByTheDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICurrencyRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        repo.Add(Currency.Create("EUR", "978", "€", string.Empty, 2, 1m, isBase: true));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
