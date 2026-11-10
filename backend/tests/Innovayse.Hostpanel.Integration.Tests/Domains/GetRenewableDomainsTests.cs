using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetRenewableDomains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="GetRenewableDomainsQuery"/> through the real <see cref="IMessageBus"/>: the active,
/// auto-renewing domains expiring inside the window.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRenewableDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A due auto-renewing domain is listed for its client; later and manual-renew domains are not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_MixedDomains_ListsOnlyDueAutoRenewingOnesAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var due = await RenewalTestSeeds.SeedActiveDomainAsync(host, DateTimeOffset.UtcNow.AddDays(5), autoRenew: true);
        var later = await RenewalTestSeeds.SeedActiveDomainAsync(host, DateTimeOffset.UtcNow.AddDays(90), autoRenew: true);
        var manual = await RenewalTestSeeds.SeedActiveDomainAsync(host, DateTimeOffset.UtcNow.AddDays(5), autoRenew: false);
        var horizon = DateTimeOffset.UtcNow.AddDays(14);

        var everyone = await InvokeAsync(host, new GetRenewableDomainsQuery(null, horizon));
        var one = await InvokeAsync(host, new GetRenewableDomainsQuery(due.ClientId, horizon));

        everyone.Select(d => d.Id).Should().Contain(due.Id).And.NotContain([later.Id, manual.Id]);
        var listed = one.Should().ContainSingle().Subject;
        listed.Name.Should().Be(due.Name);
        listed.RegistrationPeriod.Should().Be(due.RegistrationPeriod);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="query">The query to send.</param>
    /// <returns>The due domains.</returns>
    private static async Task<IReadOnlyList<RenewableDomainDto>> InvokeAsync(WebApplicationFactory<Program> host, GetRenewableDomainsQuery query)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<RenewableDomainDto>>>(query);
        return result.Value!;
    }
}
