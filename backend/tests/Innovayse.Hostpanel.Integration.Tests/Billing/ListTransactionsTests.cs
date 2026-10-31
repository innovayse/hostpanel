using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/transactions</c> against Postgres: the admin-wide, paginated transaction ledger.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTransactionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A transaction just created appears in the admin's list, named after its client.</summary>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedTransaction()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 8m);

        var response = await admin.GetAsync("/api/transactions?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("transactions").GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(transactionId);
        body.RootElement.GetProperty("transactions").GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == transactionId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>
    /// The totals add up every client's ledger in the base currency: a row in a currency worth half the base adds half
    /// its amount, and the answer names the base currency the tiles are in. Before, the global list reported zero.
    /// </summary>
    [Fact]
    public async Task TotalsAcrossAllClientsAreInTheBaseCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var before = await ReadAsync(admin, "/api/transactions?page=1&pageSize=1");
        var baseCode = before.GetProperty("currency").GetString()!;

        await SeedTransactionAsync(clientId, 100m, 10m, "ZQT");
        await SeedTransactionAsync(clientId, 10m, 0m, baseCode);
        var after = await ReadAsync(admin, "/api/transactions?page=1&pageSize=1");

        (after.GetProperty("totalIn").GetDecimal() - before.GetProperty("totalIn").GetDecimal()).Should().Be(60m);
        (after.GetProperty("totalFees").GetDecimal() - before.GetProperty("totalFees").GetDecimal()).Should().Be(5m);
        (after.GetProperty("balance").GetDecimal() - before.GetProperty("balance").GetDecimal()).Should().Be(55m);
    }

    /// <summary>A transaction recorded by hand with no invoice is in the currency its client is billed in.</summary>
    [Fact]
    public async Task ManualTransactionTakesTheClientsCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, "ZQT");
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 8m);

        var fetched = await ReadAsync(admin, $"/api/transactions/{transactionId}");

        fetched.GetProperty("currency").GetString().Should().Be("ZQT");
    }

    /// <summary>A transaction recorded against an invoice is in the invoice's currency, whatever the client is billed in now.</summary>
    [Fact]
    public async Task TransactionOfAnInvoiceTakesTheInvoiceCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedInvoiceAsync(clientId, "ZQT");

        var created = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId,
            date = DateTimeOffset.UtcNow,
            description = "Payment",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId,
            paymentMethod = "manual",
            amountIn = 20m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var transactionId = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await ReadAsync(admin, $"/api/transactions/{transactionId}");

        fetched.GetProperty("currency").GetString().Should().Be("ZQT");
    }

    /// <summary>The transaction endpoints are not for anonymous callers.</summary>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads a JSON answer from the admin API.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="url">The route to read.</param>
    /// <returns>The parsed body.</returns>
    private static async Task<JsonElement> ReadAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Writes a ledger row straight into the database, in the given currency.</summary>
    /// <param name="clientId">The client the row belongs to.</param>
    /// <param name="amountIn">The amount credited.</param>
    /// <param name="fees">The fees charged.</param>
    /// <param name="currency">ISO 4217 code the amounts are in.</param>
    private async Task SeedTransactionAsync(int clientId, decimal amountIn, decimal fees, string currency)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Transactions.Add(Transaction.Create(
            clientId, DateTimeOffset.UtcNow, "Totals test", $"tx-{Guid.NewGuid():N}", null, "manual",
            amountIn, 0m, fees, false, currency, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Writes an unpaid invoice straight into the database, in the given currency.</summary>
    /// <param name="clientId">The client the invoice is for.</param>
    /// <param name="currency">ISO 4217 code the invoice bills in.</param>
    /// <returns>The new invoice's primary key.</returns>
    private async Task<int> SeedInvoiceAsync(int clientId, string currency)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(14), currency, DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", 20m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }
}
