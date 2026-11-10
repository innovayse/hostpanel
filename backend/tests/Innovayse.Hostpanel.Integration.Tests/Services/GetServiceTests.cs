using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>GET /api/services/{id}</c>, the admin single-service detail read.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET on an unknown service answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_OnAnUnknownService_Returns404()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>GET on a service that exists returns 200 with its detail, named after its owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_OnAnExistingService_Returns200()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-service");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services/{serviceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("clientName").GetString().Should().Be("Service Owner");
    }

    /// <summary>GET on a service without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The detail names the product even when the product has no price in the caller's currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_NamesAProductWithNoPriceInTheCurrencyAsync()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "get-service-unpriced");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/services/{serviceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("productName").GetString().Should().Be("Starter");
        body.RootElement.GetProperty("billing").GetProperty("billingCycle").ValueKind.Should().Be(JsonValueKind.String);
        body.RootElement.GetProperty("automation").GetProperty("autoTerminateEndOfCycle").ValueKind.Should().Be(JsonValueKind.False);
        body.RootElement.GetProperty("account").ValueKind.Should().Be(JsonValueKind.Object);
    }
}
