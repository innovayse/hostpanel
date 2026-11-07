using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/vat-moss</c> against Postgres: VAT collected per member state, rate and
/// invoice currency for the quarter, net of the refunds made and the account credit notes issued in it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetVatMossTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A paid, taxed invoice for a member-state client appears by country code, with the quarter's bounds.</summary>
    [Fact]
    public async Task IncludesAPaidTaxedInvoiceForAMemberState()
    {
        var marker = $"Vat{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker, country: "DE");
        await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 19m, 100m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/vat-moss");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().Contain(
            r => r.GetProperty("countryCode").GetString() == "DE" && r.GetProperty("currency").GetString() == "USD");
        var now = DateTime.UtcNow;
        var quarterStart = new DateOnly(now.Year, (((now.Month - 1) / 3) * 3) + 1, 1);
        body.GetProperty("periodStart").GetString().Should().Be(quarterStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        body.GetProperty("periodEnd").GetString().Should().Be(quarterStart.AddMonths(3).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    /// <summary>A client outside the EU is not part of the VAT MOSS settlement.</summary>
    [Fact]
    public async Task LeavesOutCountriesOutsideTheEu()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatUs{Guid.NewGuid():N}", country: "US");
        await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 8m, 100m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/vat-moss");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().NotContain(r => r.GetProperty("countryCode").GetString() == "US");
    }

    /// <summary>
    /// A half refund made in the quarter takes back half of both the net value and the VAT, split in
    /// the proportion the invoice charged them, and the invoice still counts once.
    /// </summary>
    [Fact]
    public async Task NetsAPartialRefundMadeInTheQuarter()
    {
        // Only this test bills a Luxembourg client at 17%, so the row is this invoice alone.
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatLu{Guid.NewGuid():N}", country: "LU");
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 17m, 200m);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            db.Transactions.Add(invoice.AddRefund(DateTimeOffset.UtcNow, "Manual", $"rf-{Guid.NewGuid():N}", 117m, 0m, null, false, TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/vat-moss");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("countryCode").GetString() == "LU");
        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(17m);
    }

    /// <summary>
    /// An account credit note issued in the quarter takes its net value and VAT off the row of its invoice, as a refund
    /// does, though no refund row says so; the invoice still counts once. A voided note takes nothing off, and a note for
    /// the whole invoice leaves the row at zero rather than below it.
    /// </summary>
    [Fact]
    public async Task NetsAnAccountCreditNoteIssuedInTheQuarter()
    {
        // Only this test bills an Irish client at 23%, so the row is this invoice alone.
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatIe{Guid.NewGuid():N}", country: "IE");
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 23m, 100m);

        var admin = await ReportsTestSeeds.AdminClientAsync(factory);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m, nameof(CreditNoteMethod.AccountCredit));
        (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Status.Should().Be(CreditNoteStatus.Applied);

        var row = await RowAsync(admin, "IE");
        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(50m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(11.50m, "the 61.50 note carried 11.50 of VAT");

        (await admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason = "Mistake" })).EnsureSuccessStatusCode();
        (await RowAsync(admin, "IE")).GetProperty("totalVatCollected").GetDecimal().Should().Be(23m, "a voided note reverses nothing");

        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 100m, nameof(CreditNoteMethod.AccountCredit));
        var credited = await RowAsync(admin, "IE");
        credited.GetProperty("numberOfInvoices").GetInt32().Should().Be(1, "a note for the whole invoice leaves it Paid, so it is still one sale");
        credited.GetProperty("totalExclVat").GetDecimal().Should().Be(0m);
        credited.GetProperty("totalVatCollected").GetDecimal().Should().Be(0m);
    }

    /// <summary>A credit note paid out as a gateway refund comes off once, through its refund, and not a second time as a note.</summary>
    [Fact]
    public async Task NetsAGatewayRefundNoteOnce()
    {
        // Only this test bills a Maltese client at 18%, so the row is this invoice alone.
        var host = CreditNoteTestSeeds.HostWithGateway(factory, new FakePaymentPlugin { RefundResult = "gw-refund-vat-moss" });
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatMt{Guid.NewGuid():N}", country: "MT");
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m, taxRate: 18m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);

        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m, nameof(CreditNoteMethod.GatewayRefund));

        var row = await RowAsync(admin, "MT");
        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(50m, "the 59 refund gave back 50 of net value");
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(9m, "the 59 refund gave back 9 of VAT");
    }

    /// <summary>
    /// A note issued in a later quarter than its sale lowers the quarter it was issued in, as a refund does, and leaves the
    /// quarter of the sale as it was.
    /// </summary>
    [Fact]
    public async Task BooksAnAccountCreditNoteInTheQuarterItWasIssued()
    {
        // Only this test bills a Croatian client at 25%, so each row is this invoice alone.
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatHr{Guid.NewGuid():N}", country: "HR");
        var now = DateTime.UtcNow;
        var thisQuarter = new DateTimeOffset(now.Year, (((now.Month - 1) / 3) * 3) + 1, 1, 0, 0, 0, TimeSpan.Zero);
        var paidAt = thisQuarter.AddMonths(-3).AddDays(10);
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, paidAt.AddDays(7), "USD", paidAt);
            invoice.AddItem("Hosting", 100m, 1);
            invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, null, 25m);
            invoice.RecordPaidHistory($"tx-{Guid.NewGuid():N}", paidAt);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var admin = await ReportsTestSeeds.AdminClientAsync(factory);
        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m, nameof(CreditNoteMethod.AccountCredit));

        var current = await RowAsync(admin, "HR");
        current.GetProperty("numberOfInvoices").GetInt32().Should().Be(0);
        current.GetProperty("totalExclVat").GetDecimal().Should().Be(-50m);
        current.GetProperty("totalVatCollected").GetDecimal().Should().Be(-12.50m);

        var previous = await RowAsync(admin, "HR", $"?year={paidAt.Year}&quarter={((paidAt.Month - 1) / 3) + 1}");
        previous.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        previous.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        previous.GetProperty("totalVatCollected").GetDecimal().Should().Be(25m);
    }

    /// <summary>A quarter outside 1-4 is refused rather than silently answering for Q4.</summary>
    [Fact]
    public async Task RefusesAnInvalidQuarter()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/vat-moss?year=2024&quarter=5");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A year outside the supported range is refused instead of failing while the period is built.</summary>
    /// <param name="year">The year asked for.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1999)]
    [InlineData(10000)]
    public async Task RefusesAnInvalidYear(int year)
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/reports/vat-moss?year={year}&quarter=1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/vat-moss");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "vat-moss");

        var response = await client.GetAsync("/api/reports/vat-moss");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Reads the row of one member state from the report.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="countryCode">The member state's code.</param>
    /// <param name="period">The query string naming the quarter; empty for the current one.</param>
    /// <returns>The member state's only row.</returns>
    private static async Task<JsonElement> RowAsync(HttpClient admin, string countryCode, string period = "")
    {
        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/reports/vat-moss{period}");
        return body.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("countryCode").GetString() == countryCode);
    }
}
