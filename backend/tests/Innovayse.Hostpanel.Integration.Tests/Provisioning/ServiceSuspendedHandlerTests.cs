using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <c>ServiceSuspendedHandler</c>: a suspended service suspends the hosting account via the provisioning provider.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the hosting server's control-panel API) replaced by <see cref="RecordingProvisioningProvider"/>,
/// publishes the event exactly once through the real message bus and asserts the provider call happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceSuspendedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A suspended service calls the provisioning provider's suspend endpoint exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceSuspended_CallsProviderSuspendExactlyOnceAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);
        await SuspendQuietlyAsync(scope.ServiceProvider, context, serviceId);

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceSuspendedEvent(serviceId, clientId, "non-payment", DateTimeOffset.UtcNow));

        var call = Assert.Single(provider.Suspended);
        Assert.Equal(serviceId, call.ServiceId);
        Assert.Equal("provisioning-ref-1", call.ProvisioningRef);
        Assert.Equal("non-payment", call.Reason);
    }

    /// <summary>
    /// A service with no server assigned is left alone: the handler has nothing to call the
    /// provider with, and must not throw trying.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceSuspended_WithNoServerAssigned_CallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, serviceId) = await ProvisioningTestSeeds.SeedUnassignedServiceAsync(context);

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceSuspendedEvent(serviceId, clientId, "non-payment", DateTimeOffset.UtcNow));

        Assert.Empty(provider.Suspended);
    }

    /// <summary>
    /// A suspension delivered late, after the service was unsuspended again (a redelivery after a restart,
    /// or a retry behind the unsuspension), leaves the panel alone: the account the platform shows as
    /// active is not locked.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceSuspended_DeliveredAfterTheServiceIsActiveAgain_CallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceSuspendedEvent(serviceId, clientId, "non-payment", DateTimeOffset.UtcNow));

        Assert.Empty(provider.Suspended);
    }

    /// <summary>
    /// A suspension delivered after its service's row is gone, as after the demo reset truncated it,
    /// ends quietly without calling the panel.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceSuspended_ForAServiceThatNoLongerExists_CallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceSuspendedEvent(int.MaxValue, 1, "non-payment", DateTimeOffset.UtcNow));

        Assert.Empty(provider.Suspended);
    }

    /// <summary>
    /// Moves the service to suspended without raising its events, as the suspension that published the
    /// event under test already did.
    /// </summary>
    /// <param name="services">The test scope's services, for the event gate.</param>
    /// <param name="context">The data context the service was seeded through.</param>
    /// <param name="serviceId">The service to suspend.</param>
    /// <returns>A task that completes once the suspension is saved.</returns>
    private static async Task SuspendQuietlyAsync(IServiceProvider services, IHostpanelDbContext context, int serviceId)
    {
        using (services.GetRequiredService<IDomainEventGate>().Suppress())
        {
            var service = await context.ClientServices.FindAsync([serviceId], CancellationToken.None);
            service!.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
