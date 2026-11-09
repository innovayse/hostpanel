using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>ClientClosedInvoicesHandler</c>: closing an account cancels its unpaid renewal invoices and
/// leaves everything else alone (an overdue invoice is a debt on record, an invoice that is not a
/// renewal is staff's, and another client's invoice is not touched).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientClosedInvoicesHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Saves an invoice for the client, with a renewal line for a made-up service when asked.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="clientId">The client the invoice is raised for.</param>
    /// <param name="renewal">Whether the invoice carries a renewal line, or an ordinary line.</param>
    /// <param name="overdue">Whether the invoice is then marked overdue.</param>
    /// <returns>The saved invoice's id.</returns>
    private static async Task<int> SeedInvoiceAsync(IHostpanelDbContext context, int clientId, bool renewal, bool overdue = false)
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(clientId, now.AddDays(7), TestCurrencies.Base, now);
        if (renewal)
        {
            invoice.AddServiceRenewal("Renewal: Starter", 5m, 1, now.AddDays(7), now.AddDays(37));
        }
        else
        {
            invoice.AddItem("One-off setup", 5m, 1);
        }

        if (overdue)
        {
            invoice.MarkOverdue(now);
        }

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Reads an invoice's stored status.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status.</returns>
    private static Task<InvoiceStatus> StatusOfAsync(IHostpanelDbContext context, int invoiceId) =>
        context.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.Status).FirstAsync(CancellationToken.None);

    /// <summary>The closing cancels the client's unpaid renewal invoice and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientClosed_CancelsTheUnpaidRenewalInvoicesOnlyAsync()
    {
        var host = RenewalTestSeeds.SuppressedHost(factory);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var closing = await ClientsTestSeeds.SeedClientAsync(factory);
        var other = await ClientsTestSeeds.SeedClientAsync(factory);
        var unpaidRenewal = await SeedInvoiceAsync(context, closing.ClientId, renewal: true);
        var overdueRenewal = await SeedInvoiceAsync(context, closing.ClientId, renewal: true, overdue: true);
        var ordinary = await SeedInvoiceAsync(context, closing.ClientId, renewal: false);
        var othersRenewal = await SeedInvoiceAsync(context, other.ClientId, renewal: true);

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(closing.ClientId, closing.UserId, DateTimeOffset.UtcNow));

        (await StatusOfAsync(context, unpaidRenewal)).Should().Be(InvoiceStatus.Cancelled);
        (await StatusOfAsync(context, overdueRenewal)).Should().Be(InvoiceStatus.Overdue);
        (await StatusOfAsync(context, ordinary)).Should().Be(InvoiceStatus.Unpaid);
        (await StatusOfAsync(context, othersRenewal)).Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A client with no invoices closes without error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientClosed_WithNoInvoices_DoesNotThrowAsync()
    {
        using var scope = factory.Services.CreateScope();
        var closing = await ClientsTestSeeds.SeedClientAsync(factory);

        var act = () => scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(closing.ClientId, closing.UserId, DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
    }
}
