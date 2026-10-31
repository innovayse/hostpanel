using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/vat-moss</c> against Postgres: VAT collected per member state, rate and
/// invoice currency for the quarter, net of the refunds made in it.
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
}
