using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.ListEndOfCycleTerminationsDue;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="ListEndOfCycleTerminationsDueQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEndOfCycleTerminationsDueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Only a service set to terminate at the end of its cycle whose renewal date has passed is listed,
    /// with its recorded reason; one still inside its cycle and one not set to terminate are not.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task HandleAsync_MixedServices_ListsOnlyEndedCyclesSetToTerminateAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var dueBy = TestMoments.Seeded;
        var ended = await RenewalTestSeeds.SeedActiveServiceAsync(host, dueBy.AddDays(-1));
        var running = await RenewalTestSeeds.SeedActiveServiceAsync(host, dueBy.AddDays(10));
        var renewing = await RenewalTestSeeds.SeedActiveServiceAsync(host, dueBy.AddDays(-1));
        await SetToTerminateAsync(host, ended.ServiceId);
        await SetToTerminateAsync(host, running.ServiceId);

        IReadOnlyList<EndOfCycleTerminationDto> due;
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            due = (await bus.InvokeAsync<Result<IReadOnlyList<EndOfCycleTerminationDto>>>(
                new ListEndOfCycleTerminationsDueQuery(dueBy))).Value!;
        }

        due.Should().ContainEquivalentOf(new EndOfCycleTerminationDto(ended.ServiceId, "Moving on"));
        due.Select(d => d.ServiceId).Should().NotContain([running.ServiceId, renewing.ServiceId]);
    }

    /// <summary>Sets one service to terminate at the end of its cycle, straight in the database.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>A task.</returns>
    private static async Task SetToTerminateAsync(WebApplicationFactory<Program> host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId);
        service.ConfigureAutomation(service.AutoRenew, false, null, autoTerminateEndOfCycle: true, "Moving on");
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
