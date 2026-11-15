using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspensionsPastLimit;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="ListOverdueSuspensionsPastLimitQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListOverdueSuspensionsPastLimitTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Only a service suspended for non-payment before the cut-off that still renews automatically is
    /// listed; a recent one, a manual one and one billed by hand are not.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task HandleAsync_MixedSuspensions_ListsOnlyOverduePastTheCutOffAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var cutOff = TestMoments.Seeded.AddDays(-14);
        var pastLimit = await SeedSuspendedAsync(host, cutOff.AddDays(-1), SuspensionSource.Overdue, autoRenew: true);
        var recent = await SeedSuspendedAsync(host, cutOff.AddDays(1), SuspensionSource.Overdue, autoRenew: true);
        var manual = await SeedSuspendedAsync(host, cutOff.AddDays(-1), SuspensionSource.Manual, autoRenew: true);
        var billedByHand = await SeedSuspendedAsync(host, cutOff.AddDays(-1), SuspensionSource.Overdue, autoRenew: false);

        IReadOnlyList<int> ids;
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            ids = (await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListOverdueSuspensionsPastLimitQuery(cutOff))).Value!;
        }

        ids.Should().Contain(pastLimit);
        ids.Should().NotContain([recent, manual, billedByHand]);
    }

    /// <summary>Seeds an active service, then suspends it straight in the database.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="suspendedAt">The moment it is suspended at.</param>
    /// <param name="source">Why it is suspended.</param>
    /// <param name="autoRenew">Whether it still renews automatically.</param>
    /// <returns>The service id.</returns>
    private static async Task<int> SeedSuspendedAsync(
        WebApplicationFactory<Program> host, DateTimeOffset suspendedAt, SuspensionSource source, bool autoRenew)
    {
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host, suspendedAt.AddDays(30));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == seeded.ServiceId);
        service.ConfigureAutomation(autoRenew, false, null, autoTerminateEndOfCycle: false, null);
        service.Suspend(suspendedAt, hostingAlreadyUpdated: true, source: source);
        await context.SaveChangesAsync(CancellationToken.None);
        return seeded.ServiceId;
    }
}
