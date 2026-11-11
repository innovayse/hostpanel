using Innovayse.Hostpanel.Application.Services.Queries.ListUndatedRenewingServices;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="ListUndatedRenewingServicesQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListUndatedRenewingServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An active monthly service with no renewal date is listed; a dated one is not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UndatedMonthlyService_IsListedAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var undated = await RenewalTestSeeds.SeedActiveServiceAsync(host, null);
        var dated = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(10));

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var ids = (await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListUndatedRenewingServicesQuery())).Value!;

        ids.Should().Contain(undated.ServiceId);
        ids.Should().NotContain(dated.ServiceId);
    }
}
