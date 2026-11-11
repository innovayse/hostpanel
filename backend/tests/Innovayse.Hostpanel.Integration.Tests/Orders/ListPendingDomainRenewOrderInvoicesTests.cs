using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Queries.ListPendingDomainRenewOrderInvoices;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="ListPendingDomainRenewOrderInvoicesQuery"/> through the real <see cref="IMessageBus"/>:
/// the invoices of a client's pending orders that renew one domain name.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPendingDomainRenewOrderInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A pending renew order for the name is listed; a cancelled one and one for another name are not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_MixedOrders_ListsOnlyThePendingRenewalOfTheNameAsync()
    {
        int clientId;
        var name = $"{Guid.NewGuid():N}.test";
        int pendingInvoice;
        int cancelledInvoice;
        int otherNameInvoice;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            pendingInvoice = await SeedRenewOrderAsync(context, clientId, name, cancel: false);
            cancelledInvoice = await SeedRenewOrderAsync(context, clientId, name, cancel: true);
            otherNameInvoice = await SeedRenewOrderAsync(context, clientId, $"{Guid.NewGuid():N}.test", cancel: false);
        }

        using var dispatch = factory.Services.CreateScope();
        var bus = dispatch.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListPendingDomainRenewOrderInvoicesQuery(clientId, name));

        result.Value.Should().Equal(pendingInvoice);
        result.Value.Should().NotContain([cancelledInvoice, otherNameInvoice]);
    }

    /// <summary>Seeds an invoiced order with one renew line for <paramref name="domainName"/>.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="domainName">The renewed name.</param>
    /// <param name="cancel">Whether the order is cancelled.</param>
    /// <returns>The order's invoice id.</returns>
    private static async Task<int> SeedRenewOrderAsync(IHostpanelDbContext context, int clientId, string domainName, bool cancel)
    {
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem($"Domain renewal: {domainName}", 12m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..20], clientId, "bank-transfer", null, "USD", DateTimeOffset.UtcNow);
        order.AddItem(
            await DomainsTestSeeds.SeedHostingProductAsync(context), "Domain", BillingCycle.Annual, 12m, 12m, domainName, null, DomainAction.Renew, null, 1);
        order.LinkInvoice(invoice.Id);
        if (cancel)
        {
            order.Cancel();
        }

        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }
}
