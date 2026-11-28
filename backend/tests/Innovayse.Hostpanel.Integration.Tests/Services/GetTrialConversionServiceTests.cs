using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetTrialConversionService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="GetTrialConversionServiceQuery"/> against Postgres: billing reads a converted trial only while it
/// awaits its first payment, Active and with the start of its paid period, and nothing else.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTrialConversionServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A trial awaiting payment is returned with its paid period's start, anchored on that start's day.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AwaitingPaymentTrial_IsReturnedWithItsStart()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var (clientId, serviceId, _) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed(Price: 12m));
        var (start, _) = await TrialTestSeeds.BeginConversionAsync(host.Services, serviceId, TestMoments.Seeded.AddDays(20));

        var result = await QueryAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var service = result.Value!;
        service.Id.Should().Be(serviceId);
        service.ClientId.Should().Be(clientId);
        service.NextRenewalAt.Should().Be(start);
        start.Should().Be(TestMoments.Seeded.AddDays(14));
        service.AnchorDay.Should().Be(start.Day);
        service.BillingCycle.Should().Be(BillingCycle.Monthly);
        service.RecurringAmount.Should().Be(12m);
        service.AutoTerminateEndOfCycle.Should().BeFalse();
        service.HasOpenCancellation.Should().BeFalse();
    }

    /// <summary>A trial still running is not returned: it has nothing to pay yet.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ActiveTrial_IsNotReturned()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var (_, serviceId, _) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed());

        var result = await QueryAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>A service that never was a trial is not returned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrdinaryService_IsNotReturned()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, TestMoments.Seeded.AddDays(5));

        var result = await QueryAsync(host, seeded.ServiceId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>A Pending service is not returned even with its trial awaiting payment and a renewal date.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingService_IsNotReturned()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var (_, serviceId, _) = await TrialTestSeeds.SeedTrialAsync(host.Services, new TrialSeed(Activate: false));
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ClientServices.Where(s => s.Id == serviceId)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(s => s.TrialStatus, TrialStatus.AwaitingPayment)
                        .SetProperty(s => s.NextRenewalAt, TestMoments.Seeded.AddDays(14)),
                    CancellationToken.None);
        }

        var result = await QueryAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The query's result.</returns>
    private static async Task<Result<RenewableServiceDto?>> QueryAsync(WebApplicationFactory<Program> host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<RenewableServiceDto?>>(new GetTrialConversionServiceQuery(serviceId), CancellationToken.None);
    }
}
