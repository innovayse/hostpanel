using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/invoices</c> against Postgres: the paginated invoices report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoicesReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly paid invoice appears in the paginated invoices report with its client's name.</summary>
    [Fact]
    public async Task ShowsAPaidInvoice()
    {
        var marker = $"Invoice{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 42m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/invoices?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(
            i => i.GetProperty("id").GetInt32() == invoiceId
                && i.GetProperty("clientName").GetString()!.Contains(marker)
                && i.GetProperty("status").GetString() == nameof(InvoiceStatus.Paid));
    }

    /// <summary>The refund date comes from the invoice's refund ledger row; an invoice never refunded has none.</summary>
    [Fact]
    public async Task ShowsTheDayOfTheLatestRefund()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"InvoiceRefund{Guid.NewGuid():N}");
        var unrefundedId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 10m);
        int refundedId;
        var refundedAt = DateTimeOffset.UtcNow.AddDays(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 50m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddPayment(DateTimeOffset.UtcNow.AddDays(-2), "Manual", $"tx-{Guid.NewGuid():N}", 50m, 0m, null, TransactionSource.Manual));
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddRefund(
                refundedAt, PaymentMethodNames.CreditBalance, $"rf-{Guid.NewGuid():N}", 20m, 0m, null, addedToCredit: true, source: TransactionSource.Manual));
            await db.SaveChangesAsync();
            refundedId = invoice.Id;
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/invoices?page=1&pageSize=200");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Single(i => i.GetProperty("id").GetInt32() == refundedId).GetProperty("dates").GetProperty("refunded").GetString()
            .Should().Be(refundedAt.UtcDateTime.ToString("yyyy-MM-dd"));
        items.Single(i => i.GetProperty("id").GetInt32() == unrefundedId).GetProperty("dates").GetProperty("refunded").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    /// <summary>A page size outside the allowed range is refused.</summary>
    [Fact]
    public async Task RefusesAnOversizedPage()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/invoices?page=1&pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/invoices?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "invoices-report");

        var response = await client.GetAsync("/api/reports/invoices?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
