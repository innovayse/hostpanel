using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders and readers for the credit note tests: paid invoices seeded straight into the database, a host with a fake gateway
/// plugin, the issue call, and readers for the notes and the invoice a test checks.
/// </summary>
public static class CreditNoteTestSeeds
{
    /// <summary>The hosted gateway module the gateway-paid invoices in these tests were paid through.</summary>
    public const string HostedGateway = "bank-gateway";

    /// <summary>A reason that passes validation.</summary>
    public const string Reason = "Service was down for two days";

    /// <summary>Seeds a USD invoice of one line, taxed, and paid in full by a manual payment.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="price">The line price before tax.</param>
    /// <param name="taxRate">The tax percentage.</param>
    /// <returns>The paid invoice id.</returns>
    public static async Task<int> PaidInvoiceAsync(WebApplicationFactory<Program> host, int clientId, decimal price = 100m, decimal taxRate = 0m)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var invoice = NewInvoice(clientId, price, taxRate, now);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        context.Transactions.Add(invoice.AddPayment(
            now, PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", invoice.Total, 0m, null, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Seeds a USD invoice paid in full through the hosted gateway, so a gateway refund can return it.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="price">The line price before tax.</param>
    /// <returns>The paid invoice id.</returns>
    public static async Task<int> GatewayPaidInvoiceAsync(WebApplicationFactory<Program> host, int clientId, decimal price = 100m)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var invoice = NewInvoice(clientId, price, 0m, now);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        invoice.SetGatewaySession(HostedGateway, $"gw-order-{Guid.NewGuid():N}", now);
        context.Transactions.Add(invoice.Settle(HostedGateway, $"gw-pay-{Guid.NewGuid():N}", now, TransactionSource.Manual)!);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Seeds an unpaid invoice, which no credit note may be issued on.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <returns>The unpaid invoice id.</returns>
    public static async Task<int> UnpaidInvoiceAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = NewInvoice(clientId, 100m, 0m, DateTimeOffset.UtcNow);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Seeds a paid credit top-up invoice, which is credit already.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The client who topped up.</param>
    /// <returns>The paid top-up invoice id.</returns>
    public static async Task<int> PaidTopUpAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.CreateCreditTopUp(clientId, 50m, "USD", now, now.AddDays(7));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory($"top-up-{Guid.NewGuid():N}", now);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Builds a host whose gateway plugin resolver hands back the given plugin for the hosted gateway module.</summary>
    /// <param name="factory">The shared host to derive from.</param>
    /// <param name="plugin">The fake plugin the hosted gateway module resolves to.</param>
    /// <returns>The derived host.</returns>
    public static WebApplicationFactory<Program> HostWithGateway(IntegrationTestFactory factory, FakePaymentPlugin plugin)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
    }

    /// <summary>Issues a credit note of one line through the staff route.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice to credit.</param>
    /// <param name="price">The unit price of the one line, before tax.</param>
    /// <param name="method">The method name on the wire: <c>AccountCredit</c> or <c>GatewayRefund</c>.</param>
    /// <param name="reason">The reason sent.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> IssueAsync(
        HttpClient admin, int invoiceId, decimal price, string method = nameof(CreditNoteMethod.AccountCredit), string reason = Reason) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit-notes", new
        {
            items = new[] { new { description = "Credit for hosting", unitPrice = price, quantity = 1 } },
            method,
            reason,
        });

    /// <summary>Issues a credit note that must succeed and returns its id.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice to credit.</param>
    /// <param name="price">The unit price of the one line, before tax.</param>
    /// <param name="method">The method name on the wire.</param>
    /// <returns>The new note's id.</returns>
    public static async Task<int> IssueOkAsync(
        HttpClient admin, int invoiceId, decimal price, string method = nameof(CreditNoteMethod.AccountCredit))
    {
        var response = await IssueAsync(admin, invoiceId, price, method);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Reads every credit note of an invoice from the database, oldest first, with its lines.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The notes.</returns>
    public static async Task<List<CreditNote>> NotesOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CreditNotes.AsNoTracking().Include(n => n.Items).Where(n => n.InvoiceId == invoiceId).OrderBy(n => n.Id).ToListAsync();
    }

    /// <summary>Reads one credit note from the database with its lines.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="noteId">The note.</param>
    /// <returns>The note.</returns>
    public static async Task<CreditNote> NoteAsync(WebApplicationFactory<Program> host, int noteId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CreditNotes.AsNoTracking().Include(n => n.Items).SingleAsync(n => n.Id == noteId);
    }

    /// <summary>Reads an invoice from the database with its payments and refunds.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice.</returns>
    public static async Task<Invoice> InvoiceAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Seeds a draft note, which no client ever sees.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="invoiceId">The paid invoice the draft credits.</param>
    /// <returns>The draft note's id.</returns>
    public static async Task<int> DraftAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        var note = CreditNote.Create(
            invoice, [CreditNoteItem.Create("Draft line", 5m, 1, invoice.Currency)], CreditNoteMethod.AccountCredit, Reason, DateTimeOffset.UtcNow);
        context.CreditNotes.Add(note);
        await context.SaveChangesAsync(CancellationToken.None);
        return note.Id;
    }

    /// <summary>Reads the page of notes the staff list answers with.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="query">The query string such as <c>?invoiceId=3</c>.</param>
    /// <returns>The body.</returns>
    public static async Task<JsonElement> ListAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync($"/api/billing/credit-notes{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Builds the unsaved invoice the seeds start from.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="price">The line price before tax.</param>
    /// <param name="taxRate">The tax percentage.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>The Unpaid invoice.</returns>
    private static Invoice NewInvoice(int clientId, decimal price, decimal taxRate, DateTimeOffset now)
    {
        var invoice = Invoice.Create(clientId, now.AddDays(7), "USD", now);
        invoice.AddItem("Hosting", price, 1);
        invoice.UpdateOptions(now, now.AddDays(7), null, taxRate);
        return invoice;
    }
}
