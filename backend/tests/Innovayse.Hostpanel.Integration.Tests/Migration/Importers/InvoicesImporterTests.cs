using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="InvoicesImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: imports an invoice for a resolved client with
/// the source's totals and payment moment, deduplicated by the source system's own invoice id.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoicesImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An invoice for a known client is imported, with its line items.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Invoices_ImportsAnInvoiceForAKnownClientAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Invoices);
        var externalId = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("invoices", $$"""
            [{"ClientEmail":"{{email}}","ExternalId":"{{externalId}}","Total":100,"Tax":0,"TaxRate":0,"Credit":0,"Status":"Unpaid","Date":"2025-01-01T00:00:00Z","DueDate":"2025-01-15T00:00:00Z","PaidAt":null,"PaymentMethod":null,"Items":[{"Description":"Hosting","Amount":100,"Quantity":1}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var invoice = await context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.ExternalId == externalId);
        Assert.NotNull(invoice);

        // Idempotent re-import: an invoice matched by its external id is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Invoices);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var invoices = await context.Invoices.AsNoTracking().Where(i => i.ExternalId == externalId).ToListAsync();
        Assert.Single(invoices);
    }

    /// <summary>
    /// A paid invoice keeps the source's tax, credit and total, and is paid at the source's payment
    /// moment rather than at the import.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_PaidInvoice_KeepsSourceTotalsAndPaidMomentAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Invoices);
        var externalId = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("invoices", $$"""
            [{"ClientEmail":"{{email}}","ExternalId":"{{externalId}}","Total":110,"Tax":20,"TaxRate":20,"Credit":10,"Status":"Paid","Date":"2025-01-01T00:00:00Z","DueDate":"2025-01-15T00:00:00Z","PaidAt":"2025-01-10T12:00:00Z","PaymentMethod":"banktransfer","Items":[{"Description":"Hosting","Amount":100,"Quantity":1}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.ExternalId == externalId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(20m, invoice.Tax);
        Assert.Equal(10m, invoice.Credit);
        Assert.Equal(110m, invoice.Total);
        Assert.Equal(new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero), invoice.PaidAt);
        Assert.Equal(MigrationDefaults.PaidInvoiceReference, invoice.GatewayTransactionId);
    }

    /// <summary>An invoice whose source holds no due date is skipped with its own reason.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_InvoiceWithoutDueDate_IsSkippedAsDateMissingAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Invoices);
        var externalId = Guid.NewGuid().ToString("N");
        source.SetDatasetJson("invoices", $$"""
            [{"ClientEmail":"{{email}}","ExternalId":"{{externalId}}","Total":100,"Tax":0,"TaxRate":0,"Credit":0,"Status":"Unpaid","Date":"2025-01-01T00:00:00Z","DueDate":null,"PaidAt":null,"PaymentMethod":null,"Items":[{"Description":"Hosting","Amount":100,"Quantity":1}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Invoices.AsNoTracking().AnyAsync(i => i.ExternalId == externalId));
        var log = await context.MigrationLogs.AsNoTracking().SingleAsync(l => l.JobId == job.Id);
        Assert.Equal(MigrationSkipReason.DateMissing, log.SkipReason);
    }

    /// <summary>A fake source whose totals announce one invoice.</summary>
    /// <returns>The fake source.</returns>
    private static FakeMigrationPullSource NewSource() =>
        new() { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Invoices: 1) } };
}
