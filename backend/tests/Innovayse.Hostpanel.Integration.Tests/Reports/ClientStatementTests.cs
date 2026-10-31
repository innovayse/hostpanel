using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/client-statement</c> against Postgres: a client's running balance of
/// invoices and transactions.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientStatementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known client's statement includes their invoice as a debit line.</summary>
    [Fact]
    public async Task ShowsAKnownClientsInvoice()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Statement{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Domain", 15m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("clientId").GetInt32().Should().Be(clientId);
        body.GetProperty("lines").EnumerateArray().Should().Contain(l => l.GetProperty("debit").GetDecimal() == 15m);
        var line = body.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("debit").GetDecimal() == 15m);
        line.GetProperty("type").GetString().Should().Be("Invoice");
        line.GetProperty("invoiceId").GetInt32().Should().BeGreaterThan(0);
        line.GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A payment transaction appears as a Transaction line with its description, credit and the running balance.</summary>
    [Fact]
    public async Task ShowsATransactionLineWithTheRunningBalance()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"StatementTx{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Domain", 15m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Card payment", $"tx-{Guid.NewGuid():N}", null, "manual", 10m, 0m, 0m, false, "USD", TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().HaveCount(2);
        lines[0].GetProperty("type").GetString().Should().Be("Invoice");
        lines[0].GetProperty("balance").GetDecimal().Should().Be(15m);
        lines[1].GetProperty("type").GetString().Should().Be("Transaction");
        lines[1].GetProperty("invoiceId").ValueKind.Should().Be(JsonValueKind.Null);
        lines[1].GetProperty("description").GetString().Should().Be("Card payment");
        lines[1].GetProperty("debit").GetDecimal().Should().Be(0m);
        lines[1].GetProperty("credit").GetDecimal().Should().Be(10m);
        lines[1].GetProperty("balance").GetDecimal().Should().Be(5m);
        BalanceOf(body, "USD").GetProperty("endingBalance").GetDecimal().Should().Be(5m);
    }

    /// <summary>
    /// An invoice imported as already paid, with no payment recorded here, gets a matching history
    /// credit, so the statement nets to zero instead of reading as a debt.
    /// </summary>
    [Fact]
    public async Task CreditsAnInvoicePaidBeforeImport()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"StatementHist{Guid.NewGuid():N}");
        var invoiceId = await ReportsTestSeeds.CreatePaidInvoiceAsync(factory, clientId, 0m, 40m);

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().ContainSingle(l => l.GetProperty("type").GetString() == "Invoice" && l.GetProperty("debit").GetDecimal() == 40m);
        lines.Should().ContainSingle(l => l.GetProperty("type").GetString() == "PaidHistory"
            && l.GetProperty("invoiceId").GetInt32() == invoiceId
            && l.GetProperty("credit").GetDecimal() == 40m);
        BalanceOf(body, "USD").GetProperty("endingBalance").GetDecimal().Should().Be(0m);
    }

    /// <summary>A draft and a cancelled invoice ask for no money, so neither is a charge on the statement.</summary>
    [Fact]
    public async Task LeavesOutDraftAndCancelledInvoices()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"StatementDraft{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var draft = Invoice.CreateDraft(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            draft.AddItem("Draft", 11m, 1);
            var cancelled = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            cancelled.AddItem("Cancelled", 13m, 1);
            cancelled.Cancel();
            db.Invoices.AddRange(draft, cancelled);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("lines").GetArrayLength().Should().Be(0);
        body.GetProperty("balances").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A paid invoice refunded to the credit balance: the refund lowers the invoice's charge and its
    /// credit-balance ledger row is not a payout, so the statement shows the client holding the
    /// refunded amount.
    /// </summary>
    [Fact]
    public async Task RefundToTheCreditBalanceLeavesTheClientInCredit()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"StatementRefund{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 100m, 1);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"tx-{Guid.NewGuid():N}", 100m, 0m, null, TransactionSource.Manual));
            await db.SaveChangesAsync();
            db.Transactions.Add(invoice.AddRefund(
                DateTimeOffset.UtcNow, PaymentMethodNames.CreditBalance, $"rf-{Guid.NewGuid():N}", 100m, 0m, null, addedToCredit: true, source: TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().HaveCount(2);
        lines.Single(l => l.GetProperty("type").GetString() == "Invoice").GetProperty("debit").GetDecimal().Should().Be(0m);
        BalanceOf(body, "USD").GetProperty("endingBalance").GetDecimal().Should().Be(-100m);
    }

    /// <summary>
    /// A client billed in two currencies gets each line in its own currency and one balance per
    /// currency; the amounts are never added across currencies.
    /// </summary>
    [Fact]
    public async Task KeepsASeparateBalancePerCurrency()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"StatementMulti{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usd = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow.AddMinutes(-2));
            usd.AddItem("Hosting", 20m, 1);
            var eur = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "EUR", DateTimeOffset.UtcNow.AddMinutes(-1));
            eur.AddItem("Domain", 30m, 1);
            db.Invoices.AddRange(usd, eur);
            await db.SaveChangesAsync();
            db.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Card payment", $"tx-{Guid.NewGuid():N}", null, "manual", 5m, 0m, 0m, false, "EUR", TransactionSource.Manual));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync($"/api/reports/client-statement?clientId={clientId}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Select(l => l.GetProperty("currency").GetString()).Should().Equal("USD", "EUR", "EUR");
        lines[1].GetProperty("balance").GetDecimal().Should().Be(30m);
        lines[2].GetProperty("balance").GetDecimal().Should().Be(25m);
        body.GetProperty("balances").GetArrayLength().Should().Be(2);
        BalanceOf(body, "USD").GetProperty("endingBalance").GetDecimal().Should().Be(20m);
        BalanceOf(body, "EUR").GetProperty("endingBalance").GetDecimal().Should().Be(25m);
    }

    /// <summary>Finds the opening and closing balance of a currency in a statement body.</summary>
    /// <param name="body">The statement response.</param>
    /// <param name="currency">The ISO 4217 code to look up.</param>
    /// <returns>The balance entry of that currency.</returns>
    private static JsonElement BalanceOf(JsonElement body, string currency) =>
        body.GetProperty("balances").EnumerateArray().Single(b => b.GetProperty("currency").GetString() == currency);

    /// <summary>An id naming nobody is refused as CLIENT_NOT_FOUND, not a 500.</summary>
    [Fact]
    public async Task RefusesAnUnknownClient()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/client-statement?clientId=999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReportsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/client-statement?clientId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "client-statement");

        var response = await client.GetAsync("/api/reports/client-statement?clientId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A non-positive client id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    [Fact]
    public async Task ClientStatement_NonPositiveClientId_Returns400ValidationError()
    {
        var admin = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/reports/client-statement?clientId=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReportsTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
