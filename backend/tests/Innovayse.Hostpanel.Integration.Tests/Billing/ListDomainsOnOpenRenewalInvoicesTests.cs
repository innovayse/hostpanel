using Innovayse.Hostpanel.Application.Billing.Queries.ListDomainsOnOpenRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ListDomainsOnOpenRenewalInvoicesQuery"/> through the real <see cref="IMessageBus"/>: the
/// query the daily expiry check uses to skip domains already reminded by a renewal invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListDomainsOnOpenRenewalInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only the domain on an open renewal invoice is answered; a paid one and an unbilled one are not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ReturnsOnlyDomainsOnOpenRenewalInvoicesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var openDomainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var paidDomainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var unbilledDomainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);

        var open = RenewalInvoice(clientId, openDomainId);
        var paid = RenewalInvoice(clientId, paidDomainId);
        context.Invoices.AddRange(open, paid);
        await context.SaveChangesAsync(CancellationToken.None);
        paid.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(
            new ListDomainsOnOpenRenewalInvoicesQuery([openDomainId, paidDomainId, unbilledDomainId]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(openDomainId);
    }

    /// <summary>An empty id list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListDomainsOnOpenRenewalInvoicesQuery([]));

        result.Value.Should().BeEmpty();
    }

    /// <summary>Builds an Unpaid invoice with one renewal line for the domain.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="domainId">The domain renewed.</param>
    /// <returns>The unsaved invoice.</returns>
    private static Invoice RenewalInvoice(int clientId, int domainId)
    {
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(10), "USD", DateTimeOffset.UtcNow);
        var start = DateTimeOffset.UtcNow.AddDays(10);
        invoice.AddDomainRenewal("Domain renewal", 12m, domainId, start, start.AddYears(1));
        return invoice;
    }
}
