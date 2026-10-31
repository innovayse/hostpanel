using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="GetInvoiceBriefQuery"/> through the real <see cref="IMessageBus"/>: the query other
/// modules dispatch to read an invoice's status, total, amount due and currency without the Billing tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetInvoiceBriefTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An existing invoice is described with its client, status, total, amount due and currency.</summary>
    [Fact]
    public async Task HandleAsync_ExistingInvoice_ReturnsItsBriefAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 12.50m, 1);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Transactions.Add(invoice.AddPayment(
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"part-{Guid.NewGuid():N}", 2.50m, 0m, null, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var result = await InvokeAsync(new GetInvoiceBriefQuery(invoiceId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ClientId.Should().Be(clientId);
        result.Value.Status.Should().Be(InvoiceStatus.Unpaid);
        result.Value.Total.Should().Be(12.50m);
        result.Value.AmountDue.Should().Be(10m);
        result.Value.Currency.Should().Be("USD");
    }

    /// <summary>An id that names no invoice is refused with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownInvoice_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new GetInvoiceBriefQuery(BillingTestSeeds.NoSuchId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<InvoiceBriefDto>> InvokeAsync(GetInvoiceBriefQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<InvoiceBriefDto>>(query);
    }
}
