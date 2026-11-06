using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// Credit notes never double count in the income reports. <c>IncomeExtensions</c> reads the payment and refund rows of invoices and
/// no credit note, so: a credit note kept as account credit leaves income as the payment made it (the money stays taken and the
/// credit it created is a balance, not income), a credit note paid out as a gateway refund counts that refund once, a void changes
/// nothing, and the cash ledger report leaves out the credit-only line a note writes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreditNoteIncomeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sums the income rows of a client.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The income in the invoice currency.</returns>
    private static async Task<decimal> IncomeOfAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.IncomeEntries().Where(e => e.ClientId == clientId).SumAsync(e => e.Amount);
    }

    /// <summary>An account credit note, and its void, leave the income exactly as the payment made it.</summary>
    [Fact]
    public async Task AccountCreditNote_LeavesIncomeAsThePaymentMadeIt()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);
        (await IncomeOfAsync(factory, clientId)).Should().Be(120m);

        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 100m);

        (await IncomeOfAsync(factory, clientId)).Should().Be(120m);
        (await admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason = "Mistake" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await IncomeOfAsync(factory, clientId)).Should().Be(120m);
    }

    /// <summary>
    /// Credit given by a note and then spent on another invoice is not income twice: the other invoice counts only its cash, none.
    /// </summary>
    [Fact]
    public async Task SpendingTheCredit_AddsNoIncome()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-income-spend");
        var paid = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, paid, 100m);
        var next = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 30m);

        var applied = await owner.PostAsJsonAsync($"/api/me/invoices/{next}/credit", new { amount = 30m });

        applied.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await IncomeOfAsync(factory, clientId)).Should().Be(100m);
    }

    /// <summary>A note paid out as a gateway refund counts its refund once: payment 100 less the refund 40 is 60, not 20.</summary>
    [Fact]
    public async Task GatewayRefundNote_CountsTheRefundOnce()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-income" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);

        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 40m, nameof(CreditNoteMethod.GatewayRefund));

        (await IncomeOfAsync(host, clientId)).Should().Be(60m);
    }

    /// <summary>The cash ledger report lists the refund of a gateway refund note once and leaves out the credit-only line of a credit note.</summary>
    [Fact]
    public async Task TheCashLedger_ListsRefundsOnceAndLeavesOutCreditOnlyLines()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-ledger" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var refundInvoice = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);
        var creditInvoice = await CreditNoteTestSeeds.PaidInvoiceAsync(host, clientId, price: 50m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, refundInvoice, 40m, nameof(CreditNoteMethod.GatewayRefund));
        await CreditNoteTestSeeds.IssueOkAsync(admin, creditInvoice, 50m);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var cash = await context.Transactions.AsNoTracking().Where(t => t.ClientId == clientId)
            .ApplyReportFilters(null, null, null).ToListAsync();

        cash.Sum(t => t.AmountOut).Should().Be(40m, "the refund is the only money that left, and it is listed once");
        cash.Should().NotContain(t => t.CreditMovement == CreditMovementType.CreditNoteIssued);
    }
}
