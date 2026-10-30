using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for the admin <c>GET /api/provisioning/{id}/cpanel-sso</c> action.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetControlPanelSsoUrlTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can obtain a control-panel single-sign-on URL for a provisioned service.</summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_AdminCanGetUrlAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("url").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>Requesting a control-panel sign-in link for a service with no assigned server answers 400 with <c>SERVICE_NO_SERVER_ASSIGNED</c>.</summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_ServiceHasNoServer_Returns400WithServiceNoServerAssigned()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        await ProvisioningTestSeeds.ClearAssignedServerAsync(factory, serviceId);

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
    }

    /// <summary>Requesting a control-panel sign-in link for an id no service takes answers 404 with <c>SERVICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_UnknownService_Returns404WithServiceNotFound()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// A provider that refuses to issue a control-panel sign-in link answers 502 with
    /// <c>SERVICE_CPANEL_SSO_UPSTREAM_FAILED</c>.
    /// </summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_ProviderRefuses_Returns502WithServiceCpanelSsoUpstreamFailed()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();

        var failing = new FakeProvisioningProvider { ShouldFail = true };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, failing);

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CPANEL_SSO_UPSTREAM_FAILED);
    }

    /// <summary>
    /// A terminated service keeps its reference but no longer has an account, so a sign-in link is
    /// refused with 400 <c>SERVICE_NOT_ACTIVE</c> instead of asking the panel.
    /// </summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_TerminatedService_Returns400WithServiceNotActiveAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/terminate", new { reason = "test" })).EnsureSuccessStatusCode();

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>Requesting a control-panel sign-in link without authentication is refused.</summary>
    [Fact]
    public async Task GetControlPanelSsoUrl_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
