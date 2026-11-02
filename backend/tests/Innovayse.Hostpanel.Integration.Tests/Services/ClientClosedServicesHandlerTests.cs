using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>ClientClosedServicesHandler</c>: closing an account suspends its active services through the
/// suspend use case, with a source a payment never lifts, and leaves another client's services alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientClosedServicesHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads a service back from the database.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The stored status and suspension source.</returns>
    private static async Task<(ServiceStatus Status, SuspensionSource? Source)> StateOfAsync(IHostpanelDbContext context, int serviceId)
    {
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        return (service.Status, service.SuspensionSource);
    }

    /// <summary>The closing suspends the client's active service as <see cref="SuspensionSource.Other"/>.</summary>
    [Fact]
    public async Task ClientClosed_SuspendsTheActiveServicesOfThatClientOnlyAsync()
    {
        var closingServiceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        var otherServiceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var closing = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == closingServiceId, CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(closing.ClientId, "subject", DateTimeOffset.UtcNow));

        var (status, source) = await StateOfAsync(context, closingServiceId);
        status.Should().Be(ServiceStatus.Suspended);
        source.Should().Be(SuspensionSource.Other);
        (await StateOfAsync(context, otherServiceId)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A service that is not active (terminated here) is left as it is, and the handler does not throw.</summary>
    [Fact]
    public async Task ClientClosed_LeavesAServiceThatIsNotActiveAloneAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Terminated);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        var act = () => scope.ServiceProvider.PublishAndWaitAsync(new ClientClosedEvent(service.ClientId, "subject", DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
        (await StateOfAsync(context, serviceId)).Status.Should().Be(ServiceStatus.Terminated);
    }
}
