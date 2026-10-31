using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/payment</c> against Postgres: recording a manual payment marks the
/// invoice paid and writes an activity log entry, and the <c>INVOICE_NOT_FOUND</c> refusal for an
/// id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddInvoicePaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Recording a manual payment against an invoice marks it paid.</summary>
    [Fact]
    public async Task AddPaymentMarksTheInvoicePaid()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);
        var reference = $"pay-{Guid.NewGuid():N}";

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = reference,
            amount = 19.99m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await db.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == reference)).Source
                .Should().Be(TransactionSource.Manual);
        }

        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
    }

    /// <summary>
    /// Recording a manual payment writes an activity log entry against the invoice's client.
    /// </summary>
    [Fact]
    public async Task AddPayment_RecordsAnActivityLogEntryForTheInvoicesClientAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = 19.99m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        // CreateInvoiceHandler also writes an activity log entry for the same client (an "Invoice
        // Created" row), so the payment's own entry is matched by its description, not by client alone.
        var log = await context.ActivityLogs.SingleOrDefaultAsync(
            l => l.ClientId == clientId && l.Description.Contains("Added Invoice Payment"));
        log.Should().NotBeNull();
        log!.Description.Should().Contain($"Invoice ID: {invoiceId}");
    }

    /// <summary>Recording a payment against an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task AddPaymentRefusesAnUnknownInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = 10m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Recording a payment is not for anonymous callers.</summary>
    [Fact]
    public async Task AddPaymentRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = "ext",
            amount = 10m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A payment above what the invoice still owes is refused with <c>INVOICE_PAYMENT_EXCEEDS_DUE</c> and nothing is recorded.</summary>
    [Fact]
    public async Task AddPaymentAboveAmountDueRefusesWithPaymentExceedsDue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = 150m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_PAYMENT_EXCEEDS_DUE);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Transactions.AnyAsync(t => t.InvoiceId == invoiceId)).Should().BeFalse();
    }

    /// <summary>Posts a manual payment of <paramref name="amount"/> against an invoice.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount paid.</param>
    /// <returns>The HTTP response.</returns>
    private static Task<HttpResponseMessage> PayAsync(HttpClient admin, int invoiceId, decimal amount) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount,
            fees = 0m,
        });

    /// <summary>Reads the invoice's status and amount due through the admin route.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status name and the amount still due.</returns>
    private static async Task<(string Status, decimal AmountDue)> ReadAsync(HttpClient admin, int invoiceId)
    {
        using var body = JsonDocument.Parse(await (await admin.GetAsync($"/api/billing/{invoiceId}")).Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("status").GetString()!, body.RootElement.GetProperty("amountDue").GetDecimal());
    }

    /// <summary>
    /// A payment short of the total leaves the invoice Unpaid with the rest still due, and the
    /// payment that covers the rest marks it Paid with nothing due.
    /// </summary>
    [Fact]
    public async Task PartialPayments_CompleteTheInvoiceOnlyWhenTheyCoverIt()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);

        (await PayAsync(admin, invoiceId, 5m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAsync(admin, invoiceId)).Should().Be((nameof(InvoiceStatus.Unpaid), 15m));

        (await PayAsync(admin, invoiceId, 15m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAsync(admin, invoiceId)).Should().Be((nameof(InvoiceStatus.Paid), 0m));
    }

    /// <summary>A Paid invoice refuses a further payment with <c>INVOICE_NOT_PAYABLE</c> instead of recording money against it.</summary>
    [Fact]
    public async Task PaymentOnAPaidInvoice_RefusesWithInvoiceNotPayable()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        (await PayAsync(admin, invoiceId, 20m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await PayAsync(admin, invoiceId, 1m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
    }
}
