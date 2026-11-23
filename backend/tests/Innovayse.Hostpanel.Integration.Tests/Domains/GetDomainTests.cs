using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>GetDomain</c> use case: <c>GET /api/domains/{id}</c>, the
/// admin read of a single domain by primary key.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Registers a domain as admin, then calls <c>GET /api/domains/{id}</c> and expects 200 with
    /// a body that contains the registered domain name.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_AfterRegister_Returns200Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "get-domain");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        var getResponse = await admin.GetAsync($"/api/domains/{domainId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await getResponse.Content.ReadAsStringAsync();
        json.Should().Contain($"\"id\":{domainId}");
    }

    /// <summary>
    /// The read carries which optional operations the registrar that holds the domain offers, so a screen can hide the
    /// actions the registrar does not support.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_CarriesTheCapabilitiesOfTheRegistrarAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "get-domain-capabilities");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IRegistrarModuleCatalogue>(
                new FakeRegistrarModuleCatalogue(new RegistrarCapabilitiesDto(true, false, true)))));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await client.GetAsync($"/api/domains/{domainId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var capabilities = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("capabilities");
        capabilities.GetProperty("dnsManagement").GetBoolean().Should().BeTrue();
        capabilities.GetProperty("emailForwarding").GetBoolean().Should().BeFalse();
        capabilities.GetProperty("whois").GetBoolean().Should().BeTrue();
    }

    /// <summary>An id no domain ever took answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_UnknownId_Returns404Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/domains/{DomainsTestSeeds.UnknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/domains/{DomainsTestSeeds.UnknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetDomain_NonPositiveId_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
