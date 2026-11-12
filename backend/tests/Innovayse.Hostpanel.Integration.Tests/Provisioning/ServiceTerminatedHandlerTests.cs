using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <c>ServiceTerminatedHandler</c>: a terminated service terminates the hosting account via the provisioning provider.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the hosting server's control-panel API) replaced by <see cref="RecordingProvisioningProvider"/>,
/// publishes the event exactly once through the real message bus and asserts the provider call happened exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceTerminatedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A terminated service calls the provisioning provider's terminate endpoint exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceTerminated_CallsProviderTerminateExactlyOnceAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceTerminatedEvent(serviceId, clientId, "cancelled", TestMoments.Seeded));

        var call = Assert.Single(provider.Terminated);
        Assert.Equal(serviceId, call.ServiceId);
        Assert.Equal("provisioning-ref-1", call.ProvisioningRef);
        Assert.Equal("cancelled", call.Reason);
    }
}
