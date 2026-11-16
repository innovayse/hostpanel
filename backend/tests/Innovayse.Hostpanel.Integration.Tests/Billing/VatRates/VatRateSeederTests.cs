using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>
/// Integration tests for <see cref="VatRateSeeder"/>: the rates that ship with the application are in the table after startup,
/// importing again adds nothing, and a row staff changed or removed is dealt with as the seeder promises.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class VatRateSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every shipped rate is stored as a standard row after startup.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Startup_StoresEveryShippedRate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var stored = await context.VatRates.AsNoTracking().Where(r => r.Kind == VatRateKind.Standard).ToListAsync();

        foreach (var shipped in VatRateCatalogue.Rates)
        {
            stored.Should().Contain(r => r.Country == shipped.Country && r.ValidFrom == shipped.ValidFrom && r.RatePercent == shipped.RatePercent);
        }
    }

    /// <summary>The catalogue covers each of the 27 member states, and no state twice on the same day.</summary>
    [Fact]
    public void Catalogue_CoversEveryMemberStateOncePerDay()
    {
        VatRateCatalogue.Rates.Select(r => r.Country).Distinct().Should().BeEquivalentTo(EuMemberStates.Codes);
        VatRateCatalogue.Rates.GroupBy(r => (r.Country, r.ValidFrom)).Should().OnlyContain(g => g.Count() == 1);
    }

    /// <summary>Importing again adds nothing, and a rate staff edited keeps its value.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeeded_AddsNothingTwice_AndKeepsAnEditedRate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var baseline = VatRateCatalogue.Rates.First(r => r.Country == "AT");
        var row = await context.VatRates.SingleAsync(r => r.Country == "AT" && r.Kind == VatRateKind.Standard && r.ValidFrom == baseline.ValidFrom);
        var original = row.RatePercent;
        row.Update("AT", VatRateKind.Standard, 21m, baseline.ValidFrom);
        await context.SaveChangesAsync(CancellationToken.None);

        try
        {
            var added = await VatRateSeeder.EnsureSeededAsync(context, CancellationToken.None);

            added.Should().Be(0);
            var after = await context.VatRates.AsNoTracking().SingleAsync(r => r.Id == row.Id);
            after.RatePercent.Should().Be(21m);
        }
        finally
        {
            row.Update("AT", VatRateKind.Standard, original, baseline.ValidFrom);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A shipped row whose first day staff moved later is not brought back beside it: that would put two standard rates in
    /// force for the country, so the import adds nothing and the country keeps its one edited row.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeeded_DoesNotBringBackAShippedRowWhoseDayWasMoved()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var shipped = VatRateCatalogue.Rates.First(r => r.Country == "LU");
        var row = await context.VatRates.SingleAsync(r => r.Country == "LU" && r.Kind == VatRateKind.Standard && r.ValidFrom == shipped.ValidFrom);
        var moved = shipped.ValidFrom.AddMonths(6);
        row.Update("LU", VatRateKind.Standard, row.RatePercent, moved);
        await context.SaveChangesAsync(CancellationToken.None);

        try
        {
            var added = await VatRateSeeder.EnsureSeededAsync(context, CancellationToken.None);

            added.Should().Be(0);
            var rows = await context.VatRates.AsNoTracking().Where(r => r.Country == "LU" && r.Kind == VatRateKind.Standard).ToListAsync();
            rows.Select(r => r.ValidFrom).Should().Equal(moved);
        }
        finally
        {
            row.Update("LU", VatRateKind.Standard, row.RatePercent, shipped.ValidFrom);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>A later shipped rate is still brought by a release when the country holds only its earlier row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeeded_AddsALaterShippedRateToACountryHoldingOnlyTheEarlierRow()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var later = VatRateCatalogue.Rates.Where(r => r.Country == "FI").MaxBy(r => r.ValidFrom)!;
        var row = await context.VatRates.SingleAsync(r => r.Country == "FI" && r.Kind == VatRateKind.Standard && r.ValidFrom == later.ValidFrom);
        context.VatRates.Remove(row);
        await context.SaveChangesAsync(CancellationToken.None);

        var added = await VatRateSeeder.EnsureSeededAsync(context, CancellationToken.None);

        added.Should().Be(1);
        (await context.VatRates.AsNoTracking().AnyAsync(r => r.Country == "FI" && r.ValidFrom == later.ValidFrom && r.RatePercent == later.RatePercent)).Should().BeTrue();
    }

    /// <summary>A shipped row that was removed is brought back by the next import, which is how a release restores a missing rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeeded_RestoresARemovedShippedRow()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var shipped = VatRateCatalogue.Rates.First(r => r.Country == "MT");
        var row = await context.VatRates.SingleAsync(r => r.Country == "MT" && r.Kind == VatRateKind.Standard && r.ValidFrom == shipped.ValidFrom);
        context.VatRates.Remove(row);
        await context.SaveChangesAsync(CancellationToken.None);

        var added = await VatRateSeeder.EnsureSeededAsync(context, CancellationToken.None);

        added.Should().Be(1);
        (await context.VatRates.AsNoTracking().AnyAsync(r => r.Country == "MT" && r.ValidFrom == shipped.ValidFrom)).Should().BeTrue();
    }
}
