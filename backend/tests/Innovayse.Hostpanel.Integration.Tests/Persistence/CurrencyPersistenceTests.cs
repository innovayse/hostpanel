using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The data context's currency questions (<see cref="CurrencyExtensions"/> and the
/// <c>Currencies</c> table directly) against the migrated schema: the seed the
/// <c>AddCurrencies</c> migration wrote, the ordering the currency lists rely on, and the partial unique index that keeps the base currency singular.
/// </summary>
/// <remarks>
/// A real PostgreSQL rather than an in-memory provider, because two of the things worth pinning
/// only exist there (the migration's seed rows and the filtered index), and because the context's
/// full model (jsonb dictionaries on <c>TldConfig</c>) does not build on any other provider.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CurrencyPersistenceTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The migration seeded USD as the base and AMD beside it; the list is base first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MigrationSeedsUsdAsBaseAndAmdRated()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var baseCurrency = await context.GetBaseCurrencyAsync(CancellationToken.None);
        var all = await context.Currencies
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.IsBase)
            .ThenBy(c => c.Code)
            .ToListAsync(CancellationToken.None);

        baseCurrency.Code.Should().Be("USD");
        baseCurrency.RateToBase.Should().Be(1m);
        all.Select(c => c.Code).Should().StartWith("USD").And.Contain("AMD");
        all.Single(c => c.Code == "AMD").RateToBase.Should().Be(0.00256410m);
        all.Single(c => c.Code == "AMD").Decimals.Should().Be(0);
    }

    /// <summary>A lookup by code is case-insensitive, as <see cref="CurrencyExtensions.FindCurrencyAsync"/> promises.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FindAsyncIgnoresCase()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var found = await context.FindCurrencyAsync("amd", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Code.Should().Be("AMD");
    }

    /// <summary>A currency added through the context is read back by a fresh scope.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddPersistsARatedCurrency()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.Currencies.Add(Currency.Create("GBP", "826", "£", string.Empty, 2, 1.27m, isBase: false));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        using var read = factory.Services.CreateScope();
        var reader = read.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var gbp = await reader.FindCurrencyAsync("GBP", CancellationToken.None);

        gbp.Should().NotBeNull();
        gbp!.Prefix.Should().Be("£");
        gbp.RateToBase.Should().Be(1.27m);
    }

    /// <summary>The partial unique index refuses a second base row at the database, not only in code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASecondBaseIsRefusedByTheDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Currencies.Add(Currency.Create("EUR", "978", "€", string.Empty, 2, 1m, isBase: true));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
