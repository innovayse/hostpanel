using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/sales-tax</c> against Postgres: the invoices paid in a range of days, with
/// the liability totalled per invoice currency and the rows paged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSalesTaxReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A paid invoice with tax is listed with its currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IncludesAPaidTaxedInvoice()
    {
        var marker = $"Tax{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 10m, 100m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/sales-tax?pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").GetProperty("items").EnumerateArray().Should().Contain(
            r => r.GetProperty("clientName").GetString()!.Contains(marker)
                && r.GetProperty("tax").GetDecimal() == 10m
                && r.GetProperty("currency").GetString() == "USD");
    }

    /// <summary>
    /// The range is matched on the day the invoice was paid, not the day it was raised, and the
    /// totals come per currency, never added across currencies.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MatchesThePaymentDayAndTotalsPerCurrency()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"TaxDay{Guid.NewGuid():N}");
        var paidAt = new DateTimeOffset(2022, 5, 10, 23, 30, 0, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var (currency, amount) in new[] { ("SEK", 100m), ("SEK", 50m), ("NOK", 80m) })
            {
                var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), currency, DateTimeOffset.UtcNow);
                invoice.AddItem("Hosting", amount, 1);
                invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, null, 10m);
                invoice.RecordPaidHistory($"tx-{Guid.NewGuid():N}", paidAt);
                db.Invoices.Add(invoice);
            }

            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/sales-tax?from=2022-05-10&to=2022-05-10&page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var totals = body.GetProperty("totals").EnumerateArray().ToList();
        var sek = totals.Single(t => t.GetProperty("currency").GetString() == "SEK");
        sek.GetProperty("totalInvoices").GetInt32().Should().Be(2);
        sek.GetProperty("totalInvoiced").GetDecimal().Should().Be(165m);
        sek.GetProperty("taxLiability").GetDecimal().Should().Be(15m);
        totals.Single(t => t.GetProperty("currency").GetString() == "NOK").GetProperty("taxLiability").GetDecimal().Should().Be(8m);
        var rows = body.GetProperty("rows");
        rows.GetProperty("items").GetArrayLength().Should().Be(2);
        rows.GetProperty("totalCount").GetInt32().Should().Be(3);
        rows.GetProperty("items")[0].GetProperty("datePaid").GetString().Should().Be("2022-05-10");
    }

    /// <summary>
    /// The tax of an account credit note issued in the period comes off the liability of its currency: the note reversed part of the
    /// tax the invoice collected, though no refund row says so. The invoice itself is still listed and counted.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SubtractsTheTaxOfAnAccountCreditNoteIssuedInThePeriod()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"TaxNote{Guid.NewGuid():N}");
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var invoice = Invoice.Create(clientId, now.AddDays(7), "PLN", now);
            invoice.AddItem("Hosting", 100m, 1);
            invoice.UpdateOptions(now, now.AddDays(7), null, 20m);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddPayment(now, PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", invoice.Total, 0m, null, TransactionSource.Manual));
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var admin = await ReportsTestSeeds.AdminClientAsync(factory);
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var before = await PlnTotalAsync(admin, today);
        before.GetProperty("taxLiability").GetDecimal().Should().Be(20m);

        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m);

        var after = await PlnTotalAsync(admin, today);
        after.GetProperty("totalInvoices").GetInt32().Should().Be(1);
        after.GetProperty("totalInvoiced").GetDecimal().Should().Be(120m);
        after.GetProperty("taxLiability").GetDecimal().Should().Be(10m, "the 60 note carried 10 of tax");

        (await admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason = "Mistake" })).EnsureSuccessStatusCode();
        (await PlnTotalAsync(admin, today)).GetProperty("taxLiability").GetDecimal().Should().Be(20m, "a voided note reverses nothing");
    }

    /// <summary>Reads the PLN totals of the report for one day.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="day">The day, <c>yyyy-MM-dd</c>.</param>
    /// <returns>The PLN totals object.</returns>
    private static async Task<JsonElement> PlnTotalAsync(HttpClient admin, string day)
    {
        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/reports/sales-tax?from={day}&to={day}&pageSize=200");
        return body.GetProperty("totals").EnumerateArray().Single(t => t.GetProperty("currency").GetString() == "PLN");
    }

    /// <summary>A page size above the report ceiling is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnOversizedPage()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/sales-tax?pageSize=100000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/sales-tax");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "sales-tax");

        var response = await client.GetAsync("/api/reports/sales-tax");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
