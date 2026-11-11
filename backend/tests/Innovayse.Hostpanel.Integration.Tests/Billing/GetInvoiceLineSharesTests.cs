using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceLineShares;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="GetInvoiceLineSharesQuery"/> through the real <see cref="IMessageBus"/>: the query
/// another module dispatches to learn how one invoice line was paid, without the Billing tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetInvoiceLineSharesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// On a taxed invoice that used credit, a line's paid share carries its part of the tax, and its
    /// credit share and paid share add up to what the line cost.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_TaxedInvoiceWithCredit_SplitsTheLineAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Domain", 10m, 1);
            invoice.AddItem("Hosting", 30m, 1);
            invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, invoice.PaymentMethod, taxRate: 10m);
            invoice.ApplyCredit(22m);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var result = await InvokeAsync(new GetInvoiceLineSharesQuery(invoiceId, 10m));

        result.IsSuccess.Should().BeTrue();
        result.Value!.PaidShare.Should().Be(5.5m);
        result.Value.CreditShare.Should().Be(5.5m);
        result.Value.AmountRefunded.Should().Be(0m);
    }

    /// <summary>An id that names no invoice is refused with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownInvoice_RefusesNotFoundAsync()
    {
        var result = await InvokeAsync(new GetInvoiceLineSharesQuery(BillingTestSeeds.NoSuchId, 10m));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<InvoiceLineSharesDto>> InvokeAsync(GetInvoiceLineSharesQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<InvoiceLineSharesDto>>(query);
    }
}
