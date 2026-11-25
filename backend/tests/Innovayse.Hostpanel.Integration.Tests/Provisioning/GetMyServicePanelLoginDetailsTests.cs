using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for <c>GET /api/me/services/{id}/panel-login</c>: the caller reads how they sign in by hand to the panel of their
/// own provisioned service, without a password; a stranger's service is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyServicePanelLoginDetailsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner reads the login details of their own service, and no password is among them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_TheCallersOwnService_Returns200WithoutAPasswordAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var (serviceId, ownerToken) = await ProvisioningTestSeeds.SeedOwnedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await http.GetAsync($"/api/me/services/{serviceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("loginUrl").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("username").GetString().Should().NotBeNullOrEmpty();
        body.TryGetProperty("password", out _).Should().BeFalse();
    }

    /// <summary>A stranger's service is refused with the same 404 as an unknown one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_AStrangersService_Returns404WithServiceNotFoundAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var ownerServiceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{ownerServiceId}/provision", null)).EnsureSuccessStatusCode();
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.GetAsync($"/api/me/services/{ownerServiceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_Returns401_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/services/1/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
