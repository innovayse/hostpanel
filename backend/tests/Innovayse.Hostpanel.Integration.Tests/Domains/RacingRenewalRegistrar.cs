using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// A registrar that loses a renewal race on purpose: while it is asked to renew a domain set up with
/// <see cref="Race"/>, another attempt of the same order line records its renewal first (saved through a scope of its
/// own), and this call then answers with the expiry the race names. Top-level and public so Wolverine's generated handler
/// code can name it; it takes no constructor dependency, because Wolverine refuses a handler whose services it would have to
/// locate at run time, so the race carries the host the winner saves through.
/// </summary>
public sealed class RacingRenewalRegistrar : TestRegistrar
{
    /// <summary>The races set up per domain name: the host, the order line, the winner's stored expiry and this call's reported one.</summary>
    private static readonly ConcurrentDictionary<string, (IServiceProvider Services, int OrderItemId, DateTimeOffset WinnerExpiresAt, DateTimeOffset ReportedExpiresAt)> Races = new();

    /// <summary>Sets up the race for the next renewal of a domain.</summary>
    /// <param name="services">The host the winning attempt saves through.</param>
    /// <param name="domainName">The domain the race is for.</param>
    /// <param name="orderItemId">The order line both attempts renew for.</param>
    /// <param name="winnerExpiresAt">The expiry the winning attempt stores.</param>
    /// <param name="reportedExpiresAt">The expiry the registrar reports to the losing attempt.</param>
    public static void Race(
        IServiceProvider services, string domainName, int orderItemId, DateTimeOffset winnerExpiresAt, DateTimeOffset reportedExpiresAt)
    {
        Races[domainName] = (services, orderItemId, winnerExpiresAt, reportedExpiresAt);
    }

    /// <inheritdoc/>
    protected override async Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
    {
        if (!Races.TryRemove(request.DomainName, out var race))
        {
            return await stub.RenewAsync(request, ct);
        }

        using (var scope = race.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var domain = await context.Domains.Include(d => d.LineRenewals).SingleAsync(d => d.Name == request.DomainName, ct);
            domain.Renew(race.WinnerExpiresAt, DateTimeOffset.UtcNow, race.OrderItemId);
            await context.SaveChangesAsync(ct);
        }

        return new RegistrarResult(true, request.RegistrarRef, race.ReportedExpiresAt, null);
    }
}
