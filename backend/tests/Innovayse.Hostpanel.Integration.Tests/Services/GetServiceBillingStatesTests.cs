using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetServiceBillingStates;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="GetServiceBillingStatesQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceBillingStatesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A suspended service answers its status and suspension source; an unknown id is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_SuspendedAndUnknown_AnswersOnlyTheKnownStateAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(10));
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.SingleAsync(s => s.Id == seeded.ServiceId);
            service.Suspend(DateTimeOffset.UtcNow, "Overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        IReadOnlyList<ServiceBillingStateDto> states;
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<ServiceBillingStateDto>>>(
                new GetServiceBillingStatesQuery([seeded.ServiceId, int.MaxValue]));
            states = result.Value!;
        }

        var state = states.Should().ContainSingle().Subject;
        state.Id.Should().Be(seeded.ServiceId);
        state.Status.Should().Be(ServiceStatus.Suspended);
        state.SuspensionSource.Should().Be(SuspensionSource.Overdue);
        state.OverrideAutoSuspend.Should().BeFalse();
    }
}
