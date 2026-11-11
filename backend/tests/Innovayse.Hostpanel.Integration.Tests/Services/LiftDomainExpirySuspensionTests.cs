using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.LiftDomainExpirySuspension;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="LiftDomainExpirySuspensionCommand"/> through the real <see cref="IMessageBus"/>: the command
/// a renewed domain dispatches so its linked service comes back only from a suspension expiry caused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LiftDomainExpirySuspensionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A service suspended by domain expiry is active again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ExpirySuspension_UnsuspendsAsync()
    {
        var serviceId = await SeedSuspendedServiceAsync(SuspensionSource.DomainExpired);

        var result = await LiftAsync(serviceId);

        result.IsSuccess.Should().BeTrue();
        (await StatusOfAsync(serviceId)).Should().Be(ServiceStatus.Active);
    }

    /// <summary>A manual suspension is not lifted, and the command still succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ManualSuspension_LeavesItSuspendedAsync()
    {
        var serviceId = await SeedSuspendedServiceAsync(SuspensionSource.Manual);

        var result = await LiftAsync(serviceId);

        result.IsSuccess.Should().BeTrue();
        (await StatusOfAsync(serviceId)).Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>An unknown service answers <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownService_ReturnsNotFoundAsync()
    {
        var result = await LiftAsync(ServicesTestSeeds.NoSuchServiceId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Seeds an active service of a fresh client, then suspends it for the given reason.</summary>
    /// <param name="source">Why the service is suspended.</param>
    /// <returns>The service id.</returns>
    private async Task<int> SeedSuspendedServiceAsync(SuspensionSource source)
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "lift-ref", DateTimeOffset.UtcNow);
        service.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true, source: source);
        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }

    /// <summary>Dispatches the command from a fresh scope.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The command's result.</returns>
    private async Task<Result> LiftAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new LiftDomainExpirySuspensionCommand(serviceId));
    }

    /// <summary>Reads a service's status from a fresh scope.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The status as stored.</returns>
    private async Task<ServiceStatus> StatusOfAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => s.Status)
            .FirstAsync(CancellationToken.None);
    }
}
