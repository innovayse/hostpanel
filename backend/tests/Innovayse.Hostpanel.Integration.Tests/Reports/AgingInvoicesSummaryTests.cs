using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/aging-invoices-summary</c> against Postgres: totals owed per aging period
/// and currency.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AgingInvoicesSummaryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The summary totals an overdue invoice into its currency and period.</summary>
    [Fact]
    public async Task TotalsByCurrency()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/aging-invoices-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("currencies", out _).Should().BeTrue();
        body.GetProperty("periods").EnumerateArray().Select(p => p.GetProperty("period").GetString()).Should()
            .Equal("Days0To30", "Days30To60", "Days60To90", "Days90To120", "Over120");
    }

    /// <summary>
    /// An invoice is summed under its own currency, not its client's, at what it still owes after a
    /// part payment, in the period its due date falls in.
    /// </summary>
    [Fact]
    public async Task SumsTheAmountDueUnderTheInvoiceCurrency()
    {
        // A currency no other test bills in, so the sum below is this test's invoice alone.
        const string currency = "CHF";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"AgingSum{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(-75), currency, DateTimeOffset.UtcNow.AddDays(-90));
            invoice.AddItem("Hosting", 100m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"tx-{Guid.NewGuid():N}", 40m, 0m, null, TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/aging-invoices-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currencies").EnumerateArray().Select(c => c.GetString()).Should().Contain(currency);
        body.GetProperty("totals").GetProperty(currency).GetDecimal().Should().Be(60m);
        var period = body.GetProperty("periods").EnumerateArray().Single(p => p.GetProperty("period").GetString() == "Days60To90");
        period.GetProperty("amountsByCurrency").GetProperty(currency).GetDecimal().Should().Be(60m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/aging-invoices-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "aging-invoices-summary");

        var response = await client.GetAsync("/api/reports/aging-invoices-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
