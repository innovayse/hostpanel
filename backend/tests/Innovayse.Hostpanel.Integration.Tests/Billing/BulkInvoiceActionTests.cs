using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.BulkInvoiceAction;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/bulk</c> against Postgres: acting on many invoices at once, the
/// validation-time refusal for an unrecognised action, the handler's own defensive answer for the
/// same, and the count a <c>MarkPaid</c> action reports for eligible rows.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BulkInvoiceActionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The API route refuses an unrecognised action name at validation, before the handler's own
    /// defensive <c>BULK_INVOICE_ACTION_UNKNOWN</c> arm would ever run.
    /// </summary>
    [Fact]
    public async Task BulkActionRefusesAnUnknownActionNameAtValidation()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = "NotARealAction",
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// <see cref="BulkInvoiceActionHandler"/>'s own defensive answer for an action value that is not
    /// one of the ones it switches on, reachable only by calling the handler directly, since the
    /// command's validator refuses the same input first on the real API path.
    /// </summary>
    [Fact]
    public async Task HandlerAnswersBulkInvoiceActionUnknownForAnUndefinedActionValue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = new BulkInvoiceActionHandler(
            context, scope.ServiceProvider.GetRequiredService<IClientCredit>(), scope.ServiceProvider.GetRequiredService<IClock>());

        var outcome = await handler.HandleAsync(
            new BulkInvoiceActionCommand([invoiceId], (InvoiceBulkAction)99), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.BULK_INVOICE_ACTION_UNKNOWN);
    }

    /// <summary>A <c>MarkPaid</c> bulk action against a Draft invoice affects nothing and answers zero.</summary>
    [Fact]
    public async Task BulkActionMarkPaidCountsOnlyEligibleInvoices()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var draftInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 9.99m, isDraft: true);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { draftInvoiceId },
            action = "MarkPaid",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var affected = await response.Content.ReadFromJsonAsync<int>();
        affected.Should().Be(0);
    }

    /// <summary>A <c>MarkPaid</c> bulk action against an unpaid invoice marks it paid and counts it.</summary>
    [Fact]
    public async Task BulkActionMarkPaidMarksAnUnpaidInvoicePaid()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 9.99m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = "MarkPaid",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var affected = await response.Content.ReadFromJsonAsync<int>();
        affected.Should().Be(1);
    }

    /// <summary>
    /// A <c>MarkPaid</c> bulk action writes exactly one ledger transaction per settled invoice,
    /// with the invoice total, currency and the manual payment method.
    /// </summary>
    [Fact]
    public async Task BulkActionMarkPaidWritesOneLedgerTransactionPerInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, currency: "AMD");
        var first = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 500m);
        var second = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 700m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { first, second },
            action = "MarkPaid",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        foreach (var invoiceId in new[] { first, second })
        {
            var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
            var rows = await context.Transactions.AsNoTracking().Where(t => t.InvoiceId == invoiceId).ToListAsync();

            rows.Should().ContainSingle();
            rows[0].AmountIn.Should().Be(invoice.Total);
            rows[0].Currency.Should().Be("AMD");
            rows[0].PaymentMethod.Should().Be(PaymentMethodNames.Manual);
            rows[0].Source.Should().Be(TransactionSource.Manual);
        }
    }

    /// <summary>The bulk action endpoint is not for anonymous callers.</summary>
    [Fact]
    public async Task BulkActionRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { 1 },
            action = "MarkPaid",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A <c>MarkCancelled</c> bulk action skips an invoice that already took a payment, like a single cancel refuses it.</summary>
    [Fact]
    public async Task BulkActionCancelSkipsAPartPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var partPaidId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, partPaidId, 4m);
        var openId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { partPaidId, openId },
            action = "MarkCancelled",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<int>()).Should().Be(1);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var partPaid = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == partPaidId);
        partPaid.Status.Should().Be(Domain.Billing.Enums.InvoiceStatus.Unpaid);
    }

    /// <summary>A <c>MarkPaid</c> bulk action settles the rest of a part-paid invoice, so it ends paid in full.</summary>
    [Fact]
    public async Task BulkActionMarkPaidSettlesTheRestOfAPartPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 4m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = "MarkPaid",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        invoice.IsPaidInFull.Should().BeTrue();
        invoice.AmountPaid.Should().Be(10m);
    }

    /// <summary>
    /// A <c>MarkUnpaid</c> bulk action skips a paid invoice whose payment is recorded, like a single
    /// reopen refuses it: that money is undone by deleting or refunding the payment.
    /// </summary>
    [Fact]
    public async Task BulkActionMarkUnpaidSkipsAnInvoiceWithRecordedPayments()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = "MarkUnpaid",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<int>()).Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        invoice.Status.Should().Be(Domain.Billing.Enums.InvoiceStatus.Paid);
        invoice.AmountDue.Should().Be(0m);
    }

    /// <summary>
    /// A bulk delete releases the billable items that sat on the deleted invoices, so the next
    /// invoicing run bills them again.
    /// </summary>
    [Fact]
    public async Task BulkDeleteReleasesTheBillableItemsPutOnTheInvoices()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var seeded = await BillableItemSeeds.SeedItemOnACancelledInvoiceAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { seeded.InvoiceId },
            action = "Delete",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<int>()).Should().Be(1);
        var released = await BillableItemSeeds.ReadItemAsync(factory, seeded.ItemId);
        released.IsInvoiced.Should().BeFalse();
        released.InvoiceId.Should().BeNull();

        await BillableItemSeeds.RunInvoicingAsync(factory);

        (await BillableItemSeeds.ReadItemAsync(factory, seeded.ItemId)).IsInvoiced.Should().BeTrue();
    }
}
