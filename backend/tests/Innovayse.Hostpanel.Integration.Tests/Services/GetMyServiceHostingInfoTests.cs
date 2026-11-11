using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/me/services/{id}/hosting-info</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServiceHostingInfoTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The domain the seeded services carry.</summary>
    private const string ServiceDomain = "owned-by-caller.test";

    /// <summary>The owner reads the probe's answer for the domain stored on their service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HostingInfo_TheCallersOwnService_ProbesTheStoredDomainAndReturnsTheReading()
    {
        var probe = (FakeHostingProbe)factory.Services.GetRequiredService<IHostingProbe>();
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "hosting-info-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId, ServiceDomain);

        var response = await owner.GetAsync($"/api/me/services/{serviceId}/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nameservers")[0].GetString().Should().Be(FakeHostingProbe.Nameserver);
        body.GetProperty("ssl").GetProperty("issuer").GetString().Should().Be(FakeHostingProbe.Issuer);
        body.GetProperty("ssl").GetProperty("valid").GetBoolean().Should().BeTrue();
        probe.ProbedDomains.Should().Contain(ServiceDomain);
    }

    /// <summary>A service with no domain answers empty and nothing is probed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HostingInfo_AServiceWithoutADomain_ReturnsEmptyWithoutProbing()
    {
        var probe = (FakeHostingProbe)factory.Services.GetRequiredService<IHostingProbe>();
        var probedBefore = probe.ProbedDomains.Count;
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "hosting-info-nodomain");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        var response = await owner.GetAsync($"/api/me/services/{serviceId}/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nameservers").GetArrayLength().Should().Be(0);
        body.GetProperty("ssl").ValueKind.Should().Be(JsonValueKind.Null);
        probe.ProbedDomains.Count.Should().Be(probedBefore);
    }

    /// <summary>A stranger's service is refused before any probe, so the route cannot be used to look up someone else's domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HostingInfo_AStrangersService_Returns404WithServiceNotFoundAndProbesNothing()
    {
        var probe = (FakeHostingProbe)factory.Services.GetRequiredService<IHostingProbe>();
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "hosting-info-stranger-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId, "not-for-strangers.test");
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.GetAsync($"/api/me/services/{serviceId}/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        probe.ProbedDomains.Should().NotContain("not-for-strangers.test");
    }

    /// <summary>
    /// With the real probe in place, a domain that is an IP literal, a single label or an internal address
    /// is answered with the empty reading rather than being dialled or looked up.
    /// </summary>
    /// <param name="domain">The domain stored on the caller's service.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("intranet")]
    public async Task HostingInfo_AnInternalDomainOnTheService_IsRefusedByTheRealProbe(string domain)
    {
        var (owner, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "hosting-info-internal");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId, domain);
        using var realProbeHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostingProbe>();
            services.AddScoped<IHostingProbe, HostingProbe>();
        }));
        using var client = realProbeHost.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/api/me/services/{serviceId}/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nameservers").GetArrayLength().Should().Be(0);
        body.GetProperty("ssl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A non-positive id is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HostingInfo_ANonPositiveId_Returns400()
    {
        var (owner, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "hosting-info-badid");

        var response = await owner.GetAsync("/api/me/services/0/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Reading hosting info without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HostingInfo_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/services/{ServicesTestSeeds.NoSuchServiceId}/hosting-info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
