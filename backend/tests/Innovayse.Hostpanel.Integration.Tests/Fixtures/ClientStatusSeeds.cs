using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Moves a seeded client to a status through the aggregate's own methods, on a host whose
/// domain-event gate is suppressed (<see cref="RenewalTestSeeds.SuppressedHost"/>), so a closing
/// does not end the client's session or suspend its services while a guard is what is under test.
/// </summary>
internal static class ClientStatusSeeds
{
    /// <summary>Puts the client in the given status, saving without dispatching any event.</summary>
    /// <param name="factory">The shared API + PostgreSQL host; a suppressed host is derived from it.</param>
    /// <param name="clientId">The client to move.</param>
    /// <param name="status">The status to move to: Suspended, Closed or Inactive.</param>
    /// <returns>A task that completes once the status is saved.</returns>
    public static async Task SetAsync(IntegrationTestFactory factory, int clientId, ClientStatus status)
    {
        var host = RenewalTestSeeds.SuppressedHost(factory);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await context.Clients.FirstAsync(c => c.Id == clientId, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        switch (status)
        {
            case ClientStatus.Suspended:
                client.Suspend(now);
                break;
            case ClientStatus.Closed:
                client.Close(now);
                break;
            case ClientStatus.Inactive:
                client.Deactivate(now);
                break;
            case ClientStatus.Active:
                client.Activate(now);
                break;
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }
}
