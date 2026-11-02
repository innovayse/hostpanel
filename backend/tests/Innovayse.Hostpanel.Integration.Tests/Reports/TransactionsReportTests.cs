using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/transactions</c> against Postgres: the paginated transactions report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TransactionsReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly recorded transaction appears in the paginated transactions report.</summary>
    [Fact]
    public async Task ShowsATransaction()
    {
        var marker = $"Txn{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tx = Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Hosting payment", $"tx-{Guid.NewGuid():N}",
                invoiceId: null, paymentMethod: "manual", amountIn: 30m, amountOut: 0m, fees: 1m, addedToCredit: false, currency: "USD", TransactionSource.Manual);
            db.Transactions.Add(tx);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/transactions?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(
            i => i.GetProperty("clientName").GetString()!.Contains(marker)
                && i.GetProperty("amountIn").GetDecimal() == 30m);
    }

    /// <summary>A row carries the transaction's own currency and the invoice it belongs to.</summary>
    [Fact]
    public async Task ShowsTheTransactionCurrencyAndInvoice()
    {
        var marker = $"TxnCur{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 20m);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Invoice payment", $"tx-{Guid.NewGuid():N}",
                invoiceId: invoiceId, paymentMethod: "manual", amountIn: 20m, amountOut: 0m, fees: 0m, addedToCredit: false, currency: "AMD", TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/transactions?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("clientName").GetString()!.Contains(marker));
        row.GetProperty("currency").GetString().Should().Be("AMD");
        row.GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
    }

    /// <summary>The upper date bound includes the whole last day, up to its final instant.</summary>
    [Fact]
    public async Task IncludesTheWholeLastDayOfTheRange()
    {
        var marker = $"TxnDay{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        var lastMoment = new DateTimeOffset(2021, 3, 31, 23, 59, 59, 999, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Transactions.Add(Transaction.Create(
                clientId, lastMoment, "Late payment", $"tx-{Guid.NewGuid():N}",
                invoiceId: null, paymentMethod: "manual", amountIn: 7m, amountOut: 0m, fees: 0m, addedToCredit: false, currency: "USD", TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var inside = await client.GetAsync("/api/reports/transactions?dateFrom=2021-03-31&dateTo=2021-03-31&pageSize=200");
        var after = await client.GetAsync("/api/reports/transactions?dateFrom=2021-04-01&dateTo=2021-04-01&pageSize=200");

        var insideBody = await inside.Content.ReadFromJsonAsync<JsonElement>();
        insideBody.GetProperty("items").EnumerateArray().Should().Contain(i => i.GetProperty("clientName").GetString()!.Contains(marker));
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("clientName").GetString()!.Contains(marker));
    }

    /// <summary>A page size outside the allowed range is refused.</summary>
    [Fact]
    public async Task RefusesAnOversizedPage()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/transactions?page=1&pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/transactions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "transactions-report");

        var response = await client.GetAsync("/api/reports/transactions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
