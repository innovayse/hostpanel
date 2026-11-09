using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspendedServices;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="ListOverdueSuspendedServicesQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListOverdueSuspendedServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only a service suspended for an overdue invoice is listed; a manual suspension and an active service are not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_MixedSuspensions_ListsOnlyOverdueOnesAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var overdue = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(10));
        var manual = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(10));
        var active = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(10));
        await SuspendAsync(host, overdue.ServiceId, SuspensionSource.Overdue);
        await SuspendAsync(host, manual.ServiceId, SuspensionSource.Manual);

        IReadOnlyList<int> ids;
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            ids = (await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListOverdueSuspendedServicesQuery())).Value!;
        }

        ids.Should().Contain(overdue.ServiceId);
        ids.Should().NotContain([manual.ServiceId, active.ServiceId]);
    }

    /// <summary>Suspends one service straight in the database.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="source">Why it is suspended.</param>
    /// <returns>A task.</returns>
    private static async Task SuspendAsync(WebApplicationFactory<Program> host, int serviceId, SuspensionSource source)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId);
        service.Suspend(DateTimeOffset.UtcNow, "test", hostingAlreadyUpdated: true, source: source);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
