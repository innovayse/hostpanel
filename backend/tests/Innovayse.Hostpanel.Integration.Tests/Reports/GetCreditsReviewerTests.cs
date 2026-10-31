using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/credits-reviewer</c> against Postgres: the matching credits, newest first and
/// paged, with their count and sum per currency.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCreditsReviewerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A transaction added to credit appears, scoped to its client, with an ISO date and its currency.</summary>
    [Fact]
    public async Task ShowsACreditedTransaction()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Credit{Guid.NewGuid():N}");
        var date = new DateTimeOffset(2023, 2, 14, 12, 0, 0, TimeSpan.Zero);
        await AddCreditAsync(clientId, date, 25m, "USD");

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/credits-reviewer?clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("rows").GetProperty("items")[0];
        row.GetProperty("amount").GetDecimal().Should().Be(25m);
        row.GetProperty("currency").GetString().Should().Be("USD");
        row.GetProperty("date").GetString().Should().Be("2023-02-14");
        body.GetProperty("rows").GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// Credits in two currencies are totalled apart, the last day of the range counts in full, and
    /// the rows are paged.
    /// </summary>
    [Fact]
    public async Task TotalsPerCurrencyOverTheWholeLastDayAndPages()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"CreditCur{Guid.NewGuid():N}");
        var lateOnTheDay = new DateTimeOffset(2023, 6, 30, 23, 59, 30, TimeSpan.Zero);
        await AddCreditAsync(clientId, lateOnTheDay, 10m, "USD");
        await AddCreditAsync(clientId, lateOnTheDay, 15m, "USD");
        await AddCreditAsync(clientId, lateOnTheDay, 4000m, "AMD");

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync(
            $"/api/reports/credits-reviewer?clientId={clientId}&from=2023-06-30&to=2023-06-30&page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var totals = body.GetProperty("totals").EnumerateArray().ToList();
        totals.Single(t => t.GetProperty("currency").GetString() == "USD").GetProperty("totalAmount").GetDecimal().Should().Be(25m);
        totals.Single(t => t.GetProperty("currency").GetString() == "AMD").GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("rows").GetProperty("items").GetArrayLength().Should().Be(2);
        body.GetProperty("rows").GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/credits-reviewer");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "credits-reviewer");

        var response = await client.GetAsync("/api/reports/credits-reviewer");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Records a payment added to a client's credit balance.</summary>
    /// <param name="clientId">The client credited.</param>
    /// <param name="date">When the credit was added.</param>
    /// <param name="amount">The amount credited.</param>
    /// <param name="currency">The currency of the amount.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private async Task AddCreditAsync(int clientId, DateTimeOffset date, decimal amount, string currency)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Transactions.Add(Transaction.Create(
            clientId, date, "Credit added", $"tx-{Guid.NewGuid():N}",
            invoiceId: null, paymentMethod: "manual", amountIn: amount, amountOut: 0m, fees: 0m, addedToCredit: true, currency: currency, TransactionSource.Manual));
        await db.SaveChangesAsync();
    }
}
