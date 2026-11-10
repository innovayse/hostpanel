using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>ImportTldPricing</c> use case:
/// <c>POST /api/admin/tld-configs/import/{module}</c>, which resolves registrar providers
/// through a fake <see cref="IRegistrarProviderFactory"/> instead of a real registrar's HTTP
/// client.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ImportTldPricingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Builds a host whose TLD import command resolves registrar providers through
    /// <paramref name="registrars"/> instead of a real, credentialed provider.
    /// </summary>
    /// <param name="registrars">The fake factory the host should resolve registrar providers through.</param>
    /// <returns>An authenticated admin client against the fake-registrar host.</returns>
    private async Task<HttpClient> AdminClientWithFakeRegistrarAsync(FakeRegistrarProviderFactory registrars)
    {
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IRegistrarProviderFactory>(registrars)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Importing from a registrar creates new TLDs and updates the ones already configured.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ImportCreatesNewTldsAndUpdatesExistingOnesAsync()
    {
        var (tld, body) = DomainsTestSeeds.NewTldBody();
        var provider = new FakeTldPricingProvider
        {
            PricingToReturn =
            [
                new TldPricing(tld, "USD", new Dictionary<int, decimal> { [1] = 20m },
                    new Dictionary<int, decimal> { [1] = 20m }, new Dictionary<int, decimal> { [1] = 20m }),
                new TldPricing($"new-{tld}", "USD", new Dictionary<int, decimal> { [1] = 8m },
                    new Dictionary<int, decimal> { [1] = 8m }, new Dictionary<int, decimal> { [1] = 8m }),
            ],
        };
        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(RegistrarModule.NameAm, provider);
        var admin = await AdminClientWithFakeRegistrarAsync(registrars);
        (await admin.PostAsJsonAsync("/api/admin/tld-configs", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsync("/api/admin/tld-configs/import/NameAm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        result.GetProperty("imported").GetInt32().Should().Be(1);
        result.GetProperty("updated").GetInt32().Should().Be(1);
    }

    /// <summary>Importing from an unrecognized registrar module answers 400 with <c>TLD_REGISTRAR_MODULE_UNKNOWN</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ImportRefusesAnUnknownRegistrarModuleAsync()
    {
        var admin = await AdminClientWithFakeRegistrarAsync(new FakeRegistrarProviderFactory());

        var response = await admin.PostAsync("/api/admin/tld-configs/import/NoSuchRegistrar", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TLD_REGISTRAR_MODULE_UNKNOWN);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/admin/tld-configs/import/NameAm", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
