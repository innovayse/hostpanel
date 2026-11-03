using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/aging-invoices</c> against Postgres: one page of the open invoices that still
/// owe money, with the amount due in the invoice currency and the aging period.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AgingInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A part-paid EUR invoice 45 days overdue shows what it still owes, in EUR, in the 31-60 day
    /// period, with its UTC due date.
    /// </summary>
    [Fact]
    public async Task ShowsThePartPaidAmountDueInTheInvoiceCurrency()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Aging{Guid.NewGuid():N}");
        var due = DateTimeOffset.UtcNow.AddDays(-45);
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, due, "EUR", due.AddDays(-14));
            invoice.AddItem("Hosting", 42m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"tx-{Guid.NewGuid():N}", 12m, 0m, null, TransactionSource.Manual));
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/aging-invoices?pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("invoiceId").GetInt32() == invoiceId);
        row.GetProperty("currency").GetString().Should().Be("EUR");
        row.GetProperty("amountDue").GetDecimal().Should().Be(30m);
        row.GetProperty("bucket").GetString().Should().Be("Days30To60");
        row.GetProperty("dueDate").GetString().Should().Be(due.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        row.GetProperty("daysOutstanding").GetInt32().Should().Be(45);
    }

    /// <summary>The list is paged: a page size of one answers one row and the count of every open invoice.</summary>
    [Fact]
    public async Task PagesTheRows()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"AgingPage{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 2; i++)
            {
                var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(-i), "USD", DateTimeOffset.UtcNow);
                invoice.AddItem("Hosting", 5m, 1);
                db.Invoices.Add(invoice);
            }

            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/aging-invoices?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>A page size above the report ceiling is refused by the validator.</summary>
    [Fact]
    public async Task RefusesAnOversizedPage()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/aging-invoices?pageSize=100000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/aging-invoices");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "aging-invoices");

        var response = await client.GetAsync("/api/reports/aging-invoices");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
