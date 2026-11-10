using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Application.Billing.Commands.LapseBankTransfer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// A manual bank transfer nobody confirms must not pause the overdue and suspension flow for good: the daily overdue job lets a
/// transfer that waited longer than the admin's number of days lapse, so the invoice is open again (Overdue when its due date has
/// passed), and <see cref="LapseBankTransferHandler"/> refuses an invoice that waits for no transfer.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LapseBankTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A host whose mail relay is a fake and whose domain events are not published, so only the state the job leaves is observed.</summary>
    private readonly WebApplicationFactory<Program> _host = EventHandlerTestHost.WithFakeRelay(factory, new FakeEmailSender());

    /// <summary>Seeds an invoice whose client chose a bank transfer <paramref name="waitedDays"/> days ago.</summary>
    /// <param name="waitedDays">How many days ago the transfer was chosen.</param>
    /// <param name="dueInDays">When the invoice falls due, counted from now (negative for already past).</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedWaitingTransferAsync(int waitedDays, int dueInDays)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(_host);
        using var scope = _host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(clientId, now.AddDays(dueInDays), "USD", now.AddDays(-40));
        invoice.AddItem("Hosting", 25m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RequestBankTransfer(now.AddDays(-waitedDays));
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Runs one pass of the overdue job.</summary>
    /// <returns>A task that completes once the pass has ended.</returns>
    private async Task RunOverdueJobAsync()
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);
    }

    /// <summary>Reads an invoice from the database.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice as stored.</returns>
    private async Task<Invoice> LoadAsync(int invoiceId)
    {
        using var scope = _host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>A transfer that waited longer than the default fourteen days lapses back to Unpaid, and a younger one is left alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheOverdueJob_LapsesATransferThatWaitedTooLong_AndLeavesAYoungerOne()
    {
        var stale = await SeedWaitingTransferAsync(waitedDays: 15, dueInDays: 10);
        var fresh = await SeedWaitingTransferAsync(waitedDays: 2, dueInDays: 10);

        await RunOverdueJobAsync();

        var lapsed = await LoadAsync(stale);
        lapsed.Status.Should().Be(InvoiceStatus.Unpaid);
        lapsed.BankTransferRequestedAt.Should().BeNull();
        var waiting = await LoadAsync(fresh);
        waiting.Status.Should().Be(InvoiceStatus.PaymentPending);
        waiting.BankTransferRequestedAt.Should().NotBeNull();
    }

    /// <summary>A transfer that lapses after the invoice's due date returns it to Overdue, so overdue and suspension carry on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALapsedTransfer_OfAnInvoicePastItsDueDate_ReturnsToOverdue()
    {
        var invoiceId = await SeedWaitingTransferAsync(waitedDays: 20, dueInDays: -3);

        await RunOverdueJobAsync();

        var invoice = await LoadAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.BankTransferRequestedAt.Should().BeNull();
    }

    /// <summary>The number of days is the admin's: a shorter setting lapses a transfer the default would still wait for.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheAdminsNumberOfDays_DecidesWhenATransferLapses()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory, _host);
        var invoiceId = await SeedWaitingTransferAsync(waitedDays: 5, dueInDays: 10);
        try
        {
            await BankTransferTestSeeds.EnableAsync(admin, expiryDays: "3");

            await RunOverdueJobAsync();

            (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
        }
        finally
        {
            await BankTransferTestSeeds.EnableAsync(admin);
        }
    }

    /// <summary>An expiry the admin typed wrongly falls back to the default instead of lapsing every transfer at once.</summary>
    /// <param name="typed">The text the admin typed.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("0")]
    [InlineData("soon")]
    [InlineData("-4")]
    public async Task AnUnusableNumberOfDays_FallsBackToTheDefault(string typed)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory, _host);
        var invoiceId = await SeedWaitingTransferAsync(waitedDays: 5, dueInDays: 10);
        try
        {
            await BankTransferTestSeeds.EnableAsync(admin, expiryDays: typed);

            await RunOverdueJobAsync();

            (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.PaymentPending);
        }
        finally
        {
            await BankTransferTestSeeds.EnableAsync(admin);
        }
    }

    /// <summary>The command lapses a waiting transfer and refuses an invoice that waits for none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheCommand_LapsesAWaitingTransfer_AndRefusesAnInvoiceThatWaitsForNone()
    {
        var waiting = await SeedWaitingTransferAsync(waitedDays: 1, dueInDays: 10);
        var plain = await SeedWaitingTransferAsync(waitedDays: 1, dueInDays: 10);
        using (var scope = _host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            (await bus.InvokeAsync<Result>(new LapseBankTransferCommand(waiting), CancellationToken.None)).IsSuccess.Should().BeTrue();
            (await bus.InvokeAsync<Result>(new LapseBankTransferCommand(waiting), CancellationToken.None)).ErrorCode
                .Should().Be(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
            (await bus.InvokeAsync<Result>(new LapseBankTransferCommand(int.MaxValue), CancellationToken.None)).ErrorCode
                .Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        (await LoadAsync(waiting)).Status.Should().Be(InvoiceStatus.Unpaid);
        (await LoadAsync(plain)).Status.Should().Be(InvoiceStatus.PaymentPending);
    }

    /// <summary>A rejection after the due date returns the invoice to Overdue, over the real staff endpoint.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AStaffRejection_OfAnInvoicePastItsDueDate_ReturnsToOverdue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory, _host);
        var invoiceId = await SeedWaitingTransferAsync(waitedDays: 1, dueInDays: -2);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/bank-transfer/reject", new { note = "Nothing arrived." });

        response.EnsureSuccessStatusCode();
        (await LoadAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Overdue);
    }
}
