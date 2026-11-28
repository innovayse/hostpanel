using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetServiceRenewalInvoice;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="GetServiceRenewalInvoiceQuery"/> against Postgres: the newest invoice that is not cancelled and bills
/// the period of the service starting at the given moment.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceRenewalInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The invoice with the service's line for the period is found, with its status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceForThePeriod_IsFound()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        var invoiceId = await SeedInvoiceAsync(host, seeded, start, cancel: false);

        var result = await QueryAsync(host, seeded.ServiceId, start);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new ServiceRenewalInvoiceDto(invoiceId, InvoiceStatus.Unpaid));
    }

    /// <summary>Another period start finds nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherPeriodStart_FindsNothing()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        await SeedInvoiceAsync(host, seeded, start, cancel: false);

        var result = await QueryAsync(host, seeded.ServiceId, start.AddMonths(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>A cancelled invoice for the period is ignored, so the open one raised after it is found.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelledInvoice_IsIgnored()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        var openId = await SeedInvoiceAsync(host, seeded, start, cancel: false);
        await SeedInvoiceAsync(host, seeded, start, cancel: true);

        var result = await QueryAsync(host, seeded.ServiceId, start);
        result.Value.Should().Be(new ServiceRenewalInvoiceDto(openId, InvoiceStatus.Unpaid));

        var onlyCancelled = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        await SeedInvoiceAsync(host, onlyCancelled, start, cancel: true);

        (await QueryAsync(host, onlyCancelled.ServiceId, start)).Value.Should().BeNull();
    }

    /// <summary>Raises an invoice with one renewal line for the service's month from <paramref name="start"/>.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="seeded">The service.</param>
    /// <param name="start">The period start.</param>
    /// <param name="cancel">Whether the invoice is cancelled.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> SeedInvoiceAsync(WebApplicationFactory<Program> host, SeededRenewingService seeded, DateTimeOffset start, bool cancel)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(seeded.ClientId, start, "USD", TestMoments.Seeded);
        invoice.AddServiceRenewal("Renewal: Starter", 5m, seeded.ServiceId, start, start.AddMonths(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        if (cancel)
        {
            invoice.Cancel(TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return invoice.Id;
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="start">The period start.</param>
    /// <returns>The query's result.</returns>
    private static async Task<Result<ServiceRenewalInvoiceDto?>> QueryAsync(WebApplicationFactory<Program> host, int serviceId, DateTimeOffset start)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<ServiceRenewalInvoiceDto?>>(new GetServiceRenewalInvoiceQuery(serviceId, start), CancellationToken.None);
    }
}
