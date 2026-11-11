using Innovayse.Hostpanel.Application.Billing.Commands.SettleFreeInvoice;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="SettleFreeInvoiceCommand"/> through the real <see cref="IMessageBus"/>: a nothing-owed
/// invoice turns Paid with no ledger row, and one that still owes money is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SettleFreeInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A zero-total invoice is marked paid and no ledger transaction is written for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ZeroTotal_MarksPaidWithoutALedgerRowAsync()
    {
        var invoiceId = await SeedInvoiceAsync(0m);

        var result = await InvokeAsync(new SettleFreeInvoiceCommand(invoiceId));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        (await context.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoiceId)).Should().BeFalse();
    }

    /// <summary>An invoice that still owes money is refused with <c>INVOICE_NOT_PAYABLE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_AmountStillDue_RefusesNotPayableAsync()
    {
        var invoiceId = await SeedInvoiceAsync(10m);

        var result = await InvokeAsync(new SettleFreeInvoiceCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
    }

    /// <summary>Seeds an unpaid one-line invoice for a new client.</summary>
    /// <param name="amount">The line's price.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedInvoiceAsync(decimal amount)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Free Plan", amount, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(SettleFreeInvoiceCommand cmd)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }
}
