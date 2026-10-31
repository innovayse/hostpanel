using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/{id}/summary</c>: the admin dashboard summary for one client.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientSummaryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The summary groups its figures: invoices per status, transaction totals, record counts and the credit balance.</summary>
    [Fact]
    public async Task SummaryGroupsItsFigures()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}/summary");

        body.GetProperty("invoices").GetProperty("unpaid").GetProperty("count").GetInt32().Should().Be(0);
        body.GetProperty("transactions").GetProperty("netIncome").GetArrayLength().Should().Be(0);
        body.GetProperty("counts").GetProperty("totalServicesCount").GetInt32().Should().Be(0);
        body.GetProperty("creditBalance").GetProperty("amount").GetDecimal().Should().Be(0m);
        body.GetProperty("recentEmails").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A partially refunded invoice is counted under its own status, with its total, and no longer
    /// under Paid or Refunded.
    /// </summary>
    [Fact]
    public async Task SummaryCountsAPartiallyRefundedInvoiceUnderItsOwnStatus()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var now = DateTimeOffset.UtcNow;
            var invoice = Invoice.Create(clientId, now.AddDays(7), "USD", now);
            invoice.AddItem("Hosting", 40m, 1);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Transactions.Add(invoice.AddPayment(now, PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", 40m, 0m, null, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
            context.Transactions.Add(invoice.AddRefund(now, PaymentMethodNames.Manual, $"rf-{Guid.NewGuid():N}", 15m, 0m, null, false, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var invoices = (await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}/summary")).GetProperty("invoices");

        invoices.GetProperty("partiallyRefunded").GetProperty("count").GetInt32().Should().Be(1);
        var totals = invoices.GetProperty("partiallyRefunded").GetProperty("totals");
        totals.GetArrayLength().Should().Be(1);
        totals[0].GetProperty("currency").GetString().Should().Be("USD");
        totals[0].GetProperty("amount").GetDecimal().Should().Be(40m);
        invoices.GetProperty("paid").GetProperty("count").GetInt32().Should().Be(0);
        invoices.GetProperty("refunded").GetProperty("count").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// A client with paid invoices in USD and in AMD gets a separate total per currency, never one
    /// mixed sum, and the transaction figures split the same way.
    /// </summary>
    [Fact]
    public async Task SummaryKeepsTheTotalsOfEachCurrencySeparate()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var now = DateTimeOffset.UtcNow;
            foreach (var (currency, amount) in new[] { ("USD", 40m), ("AMD", 20000m), ("USD", 10m) })
            {
                var invoice = Invoice.Create(clientId, now.AddDays(7), currency, now);
                invoice.AddItem("Hosting", amount, 1);
                context.Invoices.Add(invoice);
                await context.SaveChangesAsync(CancellationToken.None);
                context.Transactions.Add(invoice.AddPayment(now, PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual));
                await context.SaveChangesAsync(CancellationToken.None);
            }
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}/summary");

        var paid = body.GetProperty("invoices").GetProperty("paid");
        paid.GetProperty("count").GetInt32().Should().Be(3);
        AmountsByCurrency(paid.GetProperty("totals")).Should().BeEquivalentTo(new Dictionary<string, decimal> { ["AMD"] = 20000m, ["USD"] = 50m });
        var transactions = body.GetProperty("transactions");
        AmountsByCurrency(transactions.GetProperty("grossRevenue")).Should().BeEquivalentTo(new Dictionary<string, decimal> { ["AMD"] = 20000m, ["USD"] = 50m });
        AmountsByCurrency(transactions.GetProperty("netIncome")).Should().BeEquivalentTo(new Dictionary<string, decimal> { ["AMD"] = 20000m, ["USD"] = 50m });
        AmountsByCurrency(transactions.GetProperty("clientExpenses")).Should().BeEquivalentTo(new Dictionary<string, decimal> { ["AMD"] = 0m, ["USD"] = 0m });
        body.GetProperty("invoices").GetProperty("unpaid").GetProperty("totals").GetArrayLength().Should().Be(0);
    }

    /// <summary>The credit balance is answered in the client's billing currency.</summary>
    [Fact]
    public async Task SummaryReportsTheCreditBalanceInTheBillingCurrency()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = await context.Clients.FirstAsync(c => c.Id == clientId);
            client.SetCurrency("AMD");
            client.AddCredit(5000m);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var credit = (await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}/summary")).GetProperty("creditBalance");

        credit.GetProperty("currency").GetString().Should().Be("AMD");
        credit.GetProperty("amount").GetDecimal().Should().Be(5000m);
    }

    /// <summary>Reads a wire list of <c>{ amount, currency }</c> entries into a map keyed by currency.</summary>
    /// <param name="list">The JSON array.</param>
    /// <returns>The amount of each currency.</returns>
    private static Dictionary<string, decimal> AmountsByCurrency(JsonElement list) =>
        list.EnumerateArray().ToDictionary(e => e.GetProperty("currency").GetString()!, e => e.GetProperty("amount").GetDecimal());

    /// <summary>The summary for a real client answers 200.</summary>
    [Fact]
    public async Task SummaryReturnsOkForARealClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The handler documents no refusal: an unknown client id answers a zero-valued summary
    /// rather than <c>CLIENT_NOT_FOUND</c>, the dashboard having never distinguished the two.
    /// </summary>
    [Fact]
    public async Task SummaryForAnUnknownClientStillAnswersOk()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Reading a client's summary is not for anonymous callers.</summary>
    [Fact]
    public async Task SummaryRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    [Fact]
    public async Task SummaryRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "summary-forbidden");

        var response = await client.GetAsync($"/api/clients/{clientId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
