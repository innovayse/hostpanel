using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="QuotesImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: imports a quote for a resolved client,
/// deduplicated by client and subject.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class QuotesImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A quote with one line item for a known client is imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Quotes_ImportsAQuoteForAKnownClientAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Quotes: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Quotes);
        var subject = $"quote-{Guid.NewGuid():N}";
        source.SetDatasetJson("quotes", $$"""
            [{"ClientEmail":"{{email}}","Subject":"{{subject}}","Stage":"Draft","ExpiryDate":"2025-06-01T00:00:00Z","ProposalText":null,"CustomerNotes":null,"Items":[{"Description":"Setup","UnitPrice":50,"Quantity":1,"DiscountPercent":0}],"CreatedAt":"2025-01-01T00:00:00Z"}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var quote = await context.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Subject == subject);
        Assert.NotNull(quote);

        // Idempotent re-import: a quote matched by client and subject is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Quotes);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var quotes = await context.Quotes.AsNoTracking().Where(q => q.Subject == subject).ToListAsync();
        Assert.Single(quotes);
    }
}
