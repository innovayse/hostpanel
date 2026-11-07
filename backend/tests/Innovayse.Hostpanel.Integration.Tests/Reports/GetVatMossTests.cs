using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

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

    /// <summary>
    /// A partial account credit note issued in the same quarter as its invoice takes its net and VAT off the row: a 40 note on a
    /// 100 invoice at 20% leaves 60 net and 12 VAT, and the invoice still counts once.
    /// </summary>
    [Fact]
    public async Task NetsACreditNoteIssuedInTheSameQuarterAsItsInvoice()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatCy{Guid.NewGuid():N}", country: "CY");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 20m, 100m, At(2031, 2, 10));
        await SeedCreditNoteAsync(invoiceId, 40m, At(2031, 3, 1));

        var row = await RowAsync(2031, 1, "CY", 20m, "USD");

        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(60m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(12m);
    }

    /// <summary>
    /// A credit note is a correction made on the day it is issued: issued in the next quarter it leaves the quarter of the sale
    /// as it was and reduces the next quarter, whose row then shows no invoices and a negative net and VAT.
    /// </summary>
    [Fact]
    public async Task BooksACreditNoteInTheQuarterItWasIssuedNotTheQuarterOfItsInvoice()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatMt{Guid.NewGuid():N}", country: "MT");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 18m, 100m, At(2032, 3, 20));
        await SeedCreditNoteAsync(invoiceId, 50m, At(2032, 4, 5));

        var sold = await RowAsync(2032, 1, "MT", 18m, "USD");
        sold.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        sold.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        sold.GetProperty("totalVatCollected").GetDecimal().Should().Be(18m);

        var corrected = await RowAsync(2032, 2, "MT", 18m, "USD");
        corrected.GetProperty("numberOfInvoices").GetInt32().Should().Be(0);
        corrected.GetProperty("totalExclVat").GetDecimal().Should().Be(-50m);
        corrected.GetProperty("totalVatCollected").GetDecimal().Should().Be(-9m);
    }

    /// <summary>Several partial notes on one invoice each take their own share; together they are less than the whole invoice.</summary>
    [Fact]
    public async Task NetsSeveralPartialCreditNotesOfOneInvoice()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatSk{Guid.NewGuid():N}", country: "SK");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 20m, 200m, At(2033, 5, 3));
        await SeedCreditNoteAsync(invoiceId, 30m, At(2033, 5, 10));
        await SeedCreditNoteAsync(invoiceId, 20m, At(2033, 6, 20));

        var row = await RowAsync(2033, 2, "SK", 20m, "USD");

        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(150m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(30m);
    }

    /// <summary>Each note reduces only the row of its own country and rate, also when one country sold at two rates.</summary>
    [Fact]
    public async Task NetsCreditNotesPerCountryAndRate()
    {
        var latvia = await ReportsTestSeeds.CreateClientAsync(factory, $"VatLv{Guid.NewGuid():N}", country: "LV");
        var estonia = await ReportsTestSeeds.CreateClientAsync(factory, $"VatEe{Guid.NewGuid():N}", country: "EE");
        var latvian = await SeedPaidInvoiceAsync(latvia, "USD", 21m, 100m, At(2034, 7, 2));
        var estonianStandard = await SeedPaidInvoiceAsync(estonia, "USD", 22m, 100m, At(2034, 7, 3));
        var estonianReduced = await SeedPaidInvoiceAsync(estonia, "USD", 9m, 100m, At(2034, 8, 4));
        await SeedCreditNoteAsync(latvian, 10m, At(2034, 9, 1));
        await SeedCreditNoteAsync(estonianStandard, 30m, At(2034, 9, 2));
        await SeedCreditNoteAsync(estonianReduced, 100m, At(2034, 9, 3));

        var lv = await RowAsync(2034, 3, "LV", 21m, "USD");
        lv.GetProperty("totalExclVat").GetDecimal().Should().Be(90m);
        lv.GetProperty("totalVatCollected").GetDecimal().Should().Be(18.9m);

        var eeStandard = await RowAsync(2034, 3, "EE", 22m, "USD");
        eeStandard.GetProperty("totalExclVat").GetDecimal().Should().Be(70m);
        eeStandard.GetProperty("totalVatCollected").GetDecimal().Should().Be(15.4m);

        var eeReduced = await RowAsync(2034, 3, "EE", 9m, "USD");
        eeReduced.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        eeReduced.GetProperty("totalExclVat").GetDecimal().Should().Be(0m);
        eeReduced.GetProperty("totalVatCollected").GetDecimal().Should().Be(0m);
    }

    /// <summary>A note in one currency reduces the row of that currency only; the same country and rate in another currency is untouched.</summary>
    [Fact]
    public async Task NetsACreditNoteInItsOwnCurrencyOnly()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatSi{Guid.NewGuid():N}", country: "SI");
        var euros = await SeedPaidInvoiceAsync(clientId, "EUR", 22m, 100m, At(2035, 10, 6));
        await SeedPaidInvoiceAsync(clientId, "USD", 22m, 100m, At(2035, 10, 7));
        await SeedCreditNoteAsync(euros, 25m, At(2035, 11, 8));

        var eur = await RowAsync(2035, 4, "SI", 22m, "EUR");
        eur.GetProperty("totalExclVat").GetDecimal().Should().Be(75m);
        eur.GetProperty("totalVatCollected").GetDecimal().Should().Be(16.5m);

        var usd = await RowAsync(2035, 4, "SI", 22m, "USD");
        usd.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        usd.GetProperty("totalVatCollected").GetDecimal().Should().Be(22m);
    }

    /// <summary>A voided note and a draft reverse nothing.</summary>
    [Fact]
    public async Task IgnoresAVoidedCreditNoteAndADraft()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatLt{Guid.NewGuid():N}", country: "LT");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 21m, 100m, At(2036, 1, 9));
        await SeedCreditNoteAsync(invoiceId, 40m, At(2036, 1, 15), CreditNoteStatus.Voided);
        await SeedCreditNoteAsync(invoiceId, 10m, At(2036, 1, 16), CreditNoteStatus.Draft);

        var row = await RowAsync(2036, 1, "LT", 21m, "USD");

        row.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(21m);
    }

    /// <summary>
    /// A gateway refund note with its refund row reduces the quarter once, by the refund row: the note itself is not read again, so
    /// a 50 note at 20% leaves 50 net and 10 VAT, not 0 and 0.
    /// </summary>
    [Fact]
    public async Task CountsAGatewayRefundNoteOnceThroughItsRefundRow()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatHr{Guid.NewGuid():N}", country: "HR");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 20m, 100m, At(2037, 1, 9));
        await SeedCreditNoteAsync(invoiceId, 50m, At(2037, 2, 9), CreditNoteStatus.Applied, CreditNoteMethod.GatewayRefund);

        var row = await RowAsync(2037, 1, "HR", 20m, "USD");

        row.GetProperty("numberOfInvoices").GetInt32().Should().Be(1);
        row.GetProperty("totalExclVat").GetDecimal().Should().Be(50m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(10m);
    }

    /// <summary>An account credit note already Applied to the client's balance still reverses its net and VAT.</summary>
    [Fact]
    public async Task NetsAnAppliedAccountCreditNote()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatBg{Guid.NewGuid():N}", country: "BG");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 20m, 100m, At(2038, 1, 9));
        await SeedCreditNoteAsync(invoiceId, 40m, At(2038, 2, 9), CreditNoteStatus.Applied);

        var row = await RowAsync(2038, 1, "BG", 20m, "USD");

        row.GetProperty("totalExclVat").GetDecimal().Should().Be(60m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(12m);
    }

    /// <summary>A note issued at the first instant of a quarter belongs to that quarter, not to the one before it.</summary>
    [Fact]
    public async Task BooksANoteIssuedAtTheFirstInstantOfAQuarterInThatQuarter()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatRo{Guid.NewGuid():N}", country: "RO");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 19m, 100m, At(2039, 3, 20));
        await SeedCreditNoteAsync(invoiceId, 50m, new DateTimeOffset(2039, 4, 1, 0, 0, 0, TimeSpan.Zero));

        var previous = await RowAsync(2039, 1, "RO", 19m, "USD");
        previous.GetProperty("totalExclVat").GetDecimal().Should().Be(100m);
        previous.GetProperty("totalVatCollected").GetDecimal().Should().Be(19m);

        var next = await RowAsync(2039, 2, "RO", 19m, "USD");
        next.GetProperty("totalExclVat").GetDecimal().Should().Be(-50m);
        next.GetProperty("totalVatCollected").GetDecimal().Should().Be(-9.5m);
    }

    /// <summary>A note whose tax rounds to zero still reduces the net, because the rate is what makes a note taxed.</summary>
    [Fact]
    public async Task NetsANoteWhoseTaxRoundedToZero()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"VatPl{Guid.NewGuid():N}", country: "PL");
        var invoiceId = await SeedPaidInvoiceAsync(clientId, "USD", 20m, 100m, At(2040, 1, 9));
        await SeedCreditNoteAsync(invoiceId, 0.01m, At(2040, 2, 9));

        var row = await RowAsync(2040, 1, "PL", 20m, "USD");

        row.GetProperty("totalExclVat").GetDecimal().Should().Be(99.99m);
        row.GetProperty("totalVatCollected").GetDecimal().Should().Be(20m);
    }

    /// <summary>Builds a UTC moment at noon of a day.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day of the month.</param>
    /// <returns>The moment.</returns>
    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Seeds an invoice with one line, taxed at the given rate and paid in full at the given moment.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="currency">The ISO 4217 code of the invoice.</param>
    /// <param name="taxRate">The tax percentage.</param>
    /// <param name="price">The line price before tax.</param>
    /// <param name="paidAt">When the payment was made (UTC).</param>
    /// <returns>The paid invoice's id.</returns>
    private async Task<int> SeedPaidInvoiceAsync(int clientId, string currency, decimal taxRate, decimal price, DateTimeOffset paidAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = Invoice.Create(clientId, paidAt.AddDays(7), currency, paidAt);
        invoice.AddItem("Hosting", price, 1);
        invoice.UpdateOptions(paidAt, paidAt.AddDays(7), null, taxRate);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        db.Transactions.Add(invoice.AddPayment(paidAt, PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", invoice.Total, 0m, null, TransactionSource.Manual));
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    /// <summary>Seeds a credit note of one line on a paid invoice, in the given state.</summary>
    /// <param name="invoiceId">The paid invoice to credit.</param>
    /// <param name="price">The unit price of the one line, before tax.</param>
    /// <param name="issuedAt">When the note is issued (UTC).</param>
    /// <param name="status">Draft, Issued, Applied or Voided; Issued by default.</param>
    /// <param name="method">Account credit by default; a gateway refund note also records its refund row, as the real use case does.</param>
    private async Task SeedCreditNoteAsync(
        int invoiceId, decimal price, DateTimeOffset issuedAt, CreditNoteStatus status = CreditNoteStatus.Issued,
        CreditNoteMethod method = CreditNoteMethod.AccountCredit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        var note = CreditNote.Create(
            invoice,
            [CreditNoteItem.Create("Credit for hosting", price, 1, invoice.Currency)],
            method,
            "Service was down",
            issuedAt);
        if (status != CreditNoteStatus.Draft)
        {
            note.Issue($"CN-T{Guid.NewGuid():N}"[..20], issuedAt);
        }

        if (method == CreditNoteMethod.GatewayRefund)
        {
            // The real use case pays the refund out first, so the refund row exists before the note is applied.
            var reference = $"rf-{Guid.NewGuid():N}";
            db.Transactions.Add(invoice.AddRefund(issuedAt, "Manual", reference, note.Total, 0m, null, false, TransactionSource.Manual));
            note.MarkApplied(issuedAt, reference);
        }
        else if (status == CreditNoteStatus.Voided)
        {
            note.Void("Mistake", issuedAt);
        }
        else if (status == CreditNoteStatus.Applied)
        {
            note.MarkApplied(issuedAt, null);
            invoice.AddCreditNoted(note.Total, issuedAt);
        }
        else if (status == CreditNoteStatus.Issued)
        {
            invoice.AddCreditNoted(note.Total, issuedAt);
        }

        db.CreditNotes.Add(note);
        await db.SaveChangesAsync();
    }

    /// <summary>Reads the one row of the report for a quarter, country, rate and currency.</summary>
    /// <param name="year">The year asked for.</param>
    /// <param name="quarter">The quarter asked for.</param>
    /// <param name="country">The member state's code.</param>
    /// <param name="rate">The VAT rate in percent.</param>
    /// <param name="currency">The ISO 4217 code of the row.</param>
    /// <returns>The row.</returns>
    private async Task<JsonElement> RowAsync(int year, int quarter, string country, decimal rate, string currency)
    {
        var admin = await ReportsTestSeeds.AdminClientAsync(factory);
        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/reports/vat-moss?year={year}&quarter={quarter}");
        return body.GetProperty("rows").EnumerateArray().Single(r =>
            r.GetProperty("countryCode").GetString() == country
            && r.GetProperty("vatRate").GetDecimal() == rate
            && r.GetProperty("currency").GetString() == currency);
    }
}
