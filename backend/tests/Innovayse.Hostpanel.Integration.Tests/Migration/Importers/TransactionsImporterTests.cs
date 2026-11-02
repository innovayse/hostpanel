using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="TransactionsImporter"/>, driven end to end through
/// <c>MigrationPullWorker.RunAsync</c> against a <see cref="FakeMigrationPullSource"/>: imports a
/// client ledger transaction, deduplicated per client by the source system's transaction
/// reference, and links it to the invoice imported from the source invoice it pays.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TransactionsImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A transaction for a known client is imported.</summary>
    [Fact]
    public async Task RunAsync_Transactions_ImportsATransactionForAKnownClientAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        var transactionId = $"TX-{Guid.NewGuid():N}";
        source.SetDatasetJson("transactions", TransactionJson(email, transactionId, invoiceId: null));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var transaction = await context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.TransactionId == transactionId);
        Assert.NotNull(transaction);
        Assert.Null(transaction.InvoiceId);
        Assert.Equal(TransactionSource.Import, transaction.Source);

        // Idempotent re-import: a transaction matched by its id is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var transactions = await context.Transactions.AsNoTracking().Where(t => t.TransactionId == transactionId).ToListAsync();
        Assert.Single(transactions);
    }

    /// <summary>
    /// The source invoice number is resolved to the local invoice imported from it, never used as
    /// a local invoice id itself.
    /// </summary>
    [Fact]
    public async Task RunAsync_TransactionForImportedInvoice_LinksTheLocalInvoiceAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var clientId = await context.Clients.AsNoTracking()
            .Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync();
        var sourceInvoiceId = Guid.NewGuid().ToString("N");
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.SetExternalId(sourceInvoiceId);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        var transactionId = $"TX-{Guid.NewGuid():N}";
        source.SetDatasetJson("transactions", TransactionJson(email, transactionId, sourceInvoiceId));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var transaction = await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId);
        Assert.Equal(invoice.Id, transaction.InvoiceId);
    }

    /// <summary>A transaction paying a source invoice that was not imported is skipped with its own reason.</summary>
    [Fact]
    public async Task RunAsync_TransactionForMissingInvoice_IsSkippedAsInvoiceNotFoundAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        var transactionId = $"TX-{Guid.NewGuid():N}";
        source.SetDatasetJson("transactions", TransactionJson(email, transactionId, Guid.NewGuid().ToString("N")));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Transactions.AsNoTracking().AnyAsync(t => t.TransactionId == transactionId));
        var log = await context.MigrationLogs.AsNoTracking().SingleAsync(l => l.JobId == job.Id);
        Assert.Equal(MigrationSkipReason.InvoiceNotFound, log.SkipReason);
    }

    /// <summary>A transaction whose source currency is not a three-letter code is skipped, not guessed at.</summary>
    [Fact]
    public async Task RunAsync_TransactionWithMalformedCurrency_IsSkippedAsCurrencyInvalidAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        var transactionId = $"TX-{Guid.NewGuid():N}";
        source.SetDatasetJson("transactions", TransactionJson(email, transactionId, invoiceId: null, currency: "12!"));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Transactions.AsNoTracking().AnyAsync(t => t.TransactionId == transactionId));
        var log = await context.MigrationLogs.AsNoTracking().SingleAsync(l => l.JobId == job.Id);
        Assert.Equal(MigrationSkipReason.CurrencyInvalid, log.SkipReason);
    }

    /// <summary>A padded, lower-case source currency is imported trimmed and upper-cased.</summary>
    [Fact]
    public async Task RunAsync_TransactionWithPaddedCurrency_IsImportedNormalisedAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Transactions);
        var transactionId = $"TX-{Guid.NewGuid():N}";
        source.SetDatasetJson("transactions", TransactionJson(email, transactionId, invoiceId: null, currency: " usd "));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var transaction = await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == transactionId);
        Assert.Equal("USD", transaction.Currency);
    }

    /// <summary>A fake source whose totals announce one transaction.</summary>
    /// <returns>The fake source.</returns>
    private static FakeMigrationPullSource NewSource() =>
        new() { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Transactions: 1) } };

    /// <summary>The <c>transactions</c> dataset holding one payment of 10.</summary>
    /// <param name="email">The owning client's email.</param>
    /// <param name="transactionId">The gateway reference of the payment.</param>
    /// <param name="invoiceId">The source invoice number it pays, or null when unlinked.</param>
    /// <param name="currency">The source currency text, or null when the source sends none.</param>
    /// <returns>The dataset JSON.</returns>
    private static string TransactionJson(string email, string transactionId, string? invoiceId, string? currency = null)
    {
        var invoice = invoiceId is null ? "null" : $"\"{invoiceId}\"";
        var currencyJson = currency is null ? "null" : $"\"{currency}\"";
        return $$"""
            [{"ClientEmail":"{{email}}","ExternalId":"1","Date":"2025-01-01T00:00:00Z","Description":"Payment","TransactionId":"{{transactionId}}","InvoiceId":{{invoice}},"PaymentMethod":"manual","AmountIn":10,"AmountOut":0,"Fees":0,"Currency":{{currencyJson}}}]
            """;
    }
}
