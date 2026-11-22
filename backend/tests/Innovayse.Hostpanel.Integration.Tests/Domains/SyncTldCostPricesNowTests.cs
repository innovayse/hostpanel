using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>SyncTldCostPricesNow</c> use case: the admin-triggered one-pass sync
/// behind <c>POST /api/admin/tld-configs/sync</c>. The scheduled daily run is
/// <c>SyncTldCostPrices</c>, tested in <see cref="SyncTldCostPricesTests"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncTldCostPricesNowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Triggering a manual sync through the admin endpoint succeeds and reaches every configured registrar.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SyncSucceedsAndPollsEveryConfiguredRegistrarAsync()
    {
        var (_, body) = DomainsTestSeeds.NewTldBody();
        var provider = new FakeTldPricingProvider();
        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(TestRegistrarModules.First, provider);

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IRegistrarProviderFactory>(registrars)));
        var token = await factory.GetAdminTokenAsync();
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await admin.PostAsJsonAsync("/api/admin/tld-configs", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsync("/api/admin/tld-configs/sync", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        provider.PricingCallCount.Should().Be(1);
    }

    /// <summary>An unauthenticated caller is refused with 401 on the admin-triggered sync.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Sync_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/admin/tld-configs/sync", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
