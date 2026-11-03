using System.Net;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/transactions/export</c> against Postgres: the transactions report as a
/// CSV file.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ExportTransactionsReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Transactions CSV export answers with the expected headers, content type and filename.</summary>
    [Fact]
    public async Task ReturnsTheExpectedCsvShape()
    {
        var marker = $"ExportTxn{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tx = Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Export test", $"tx-{Guid.NewGuid():N}",
                invoiceId: null, paymentMethod: "manual", amountIn: 12m, amountOut: 0m, fees: 0m, addedToCredit: false, currency: "USD", TransactionSource.Manual);
            db.Transactions.Add(tx);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("transactions-report.csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith(
            "ID,Client ID,Client Name,Currency,Payment Method,Date,Description,Invoice ID,Transaction ID,Amount In,Fees,Amount Out");
        csv.Should().Contain(marker);
    }

    /// <summary>
    /// A row is written with the transaction's own currency and invoice id, and a description that
    /// would run as a spreadsheet formula is defused.
    /// </summary>
    [Fact]
    public async Task WritesTheCurrencyAndDefusesAFormula()
    {
        var marker = $"ExportTxnCur{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 9m);
        var reference = $"tx-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "=HYPERLINK(\"x\")", reference,
                invoiceId: invoiceId, paymentMethod: "manual", amountIn: 9m, amountOut: 0m, fees: 0m, addedToCredit: false, currency: "EUR", TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/transactions/export");

        var csv = await response.Content.ReadAsStringAsync();
        var line = csv.Split("\r\n").Single(l => l.Contains(reference, StringComparison.Ordinal));
        line.Should().Contain(",EUR,manual,");
        line.Should().Contain("\"'=HYPERLINK(\"\"x\"\")\"");
        line.Should().Contain($",{invoiceId},{reference},9");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "export-transactions-report");

        var response = await client.GetAsync("/api/reports/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
