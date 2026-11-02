using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Tests for <c>ClientServiceTerminatedHandler</c>: a termination, whatever path raised it, closes
/// the service's open cancellation requests.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientServiceTerminatedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The termination event closes the open request of the terminated service.</summary>
    [Fact]
    public async Task ClientServiceTerminated_ClosesTheOpenCancellationRequest()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Terminated);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.CancellationRequests.Add(CancellationRequest.Create(serviceId, CancellationType.EndOfBillingPeriod, null));
        await context.SaveChangesAsync(CancellationToken.None);
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientServiceTerminatedEvent(serviceId, service.ClientId, DateTimeOffset.UtcNow));

        (await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.ServiceId == serviceId, CancellationToken.None))
            .Status.Should().Be(CancellationStatus.Closed);
    }

    /// <summary>A refusal from the command (the service is unknown) is logged, not thrown into the event chain.</summary>
    [Fact]
    public async Task ClientServiceTerminated_UnknownServiceDoesNotThrow()
    {
        using var scope = factory.Services.CreateScope();

        var act = () => scope.ServiceProvider.PublishAndWaitAsync(
            new ClientServiceTerminatedEvent(ServicesTestSeeds.NoSuchServiceId, 1, DateTimeOffset.UtcNow));

        await act.Should().NotThrowAsync();
    }
}
