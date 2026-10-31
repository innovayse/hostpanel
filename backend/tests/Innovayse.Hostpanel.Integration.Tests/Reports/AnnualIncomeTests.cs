using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/annual-income</c> against Postgres: twelve months of paid-invoice income.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AnnualIncomeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Twelve months come back regardless of whether anything was paid.</summary>
    [Fact]
    public async Task ReturnsTwelveMonths()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/annual-income?year=2020");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows.Should().HaveCount(12);
    }

    /// <summary>
    /// Income is payments less refunds in the month the money moved (a payment in the last second of
    /// January stays in January), an invoice settled without a payment counts its total when paid,
    /// credit is not income, and the foreign invoice is converted to the base currency.
    /// </summary>
    [Fact]
    public async Task SumsIncomeByTheMonthTheMoneyMoved()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2031);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/annual-income?year=2031");

        rows!.Select(r => r.GetProperty("amount").GetDecimal())
            .Should().Equal(100m, 300m, -50m, 40m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        rows.Should().OnlyContain(r => r.GetProperty("currency").GetString() == scenario.BaseCurrency);
    }

    /// <summary>
    /// An imported paid invoice that is then fully refunded nets to zero: it keeps its paid moment, so
    /// its total and its refund cancel out instead of leaving the refund as negative income.
    /// </summary>
    [Fact]
    public async Task NetsAFullyRefundedHistoryInvoiceToZero()
    {
        const int year = 2032;
        await SeedRefundedHistoryInvoiceAsync(year);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var rows = await client.GetFromJsonAsync<List<JsonElement>>($"/api/reports/annual-income?year={year}");

        rows!.Select(r => r.GetProperty("amount").GetDecimal()).Should().OnlyContain(amount => amount == 0m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/annual-income?year=2020");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "annual-income");

        var response = await client.GetAsync("/api/reports/annual-income?year=2020");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Seeds an invoice settled as imported history on 5 February and fully refunded on 10 February.</summary>
    /// <param name="year">The year the movements fall in; one no other test of the class uses.</param>
    /// <returns>A task that completes once the invoice is saved.</returns>
    private async Task SeedRefundedHistoryInvoiceAsync(int year)
    {
        using var scope = factory.Services.CreateScope();
        // Seeded history, not live payments: no mail, provisioning or renewal may follow from it.
        using var suppression = scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var baseCurrency = await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None);
        var payer = Client.Create(
            $"refunded-{Guid.NewGuid():N}", "Refunded", "Payer", $"refunded-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(payer);
        await context.SaveChangesAsync(CancellationToken.None);

        var issued = IncomeTestSeeds.At(year, 1, 1);
        var invoice = Invoice.Create(payer.Id, issued.AddDays(14), baseCurrency.Code, issued);
        invoice.AddItem("Hosting", 40m, 1, null);
        invoice.Backdate(issued);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        invoice.RecordPaidHistory($"tx-{Guid.NewGuid():N}", IncomeTestSeeds.At(year, 2, 5));
        context.Transactions.Add(invoice.AddRefund(IncomeTestSeeds.At(year, 2, 10), "Card", $"tx-{Guid.NewGuid():N}", 40m, 0m, null, false, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
