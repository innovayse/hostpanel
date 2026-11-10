using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;
using Innovayse.Hostpanel.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for the admin <c>GET /api/provisioning/{id}/cpanel-sso</c> action.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetControlPanelSsoUrlTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can obtain a control-panel single-sign-on URL for a provisioned service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// A provider that names an address not on its own server (its coded refusal) answers 502 with
    /// <c>SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED</c>, and only that code, whatever the provider's text.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_ProviderRejectsTheAddress_Returns502WithTheUrlCodeAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var rejecting = new FakeProvisioningProvider
        {
            FailWith = new DomainRuleException(HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED, "https://evil.example/ is not on the server"),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, rejecting);

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("evil.example");
    }

    /// <summary>
    /// The handler asks the provider for a link bound to the caller's resolved address: a panel that
    /// binds its session to the browser (Plesk) depends on it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_PassesTheCallersAddressToTheProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);
        var provider = new RecordingProvisioningProvider();
        var handler = new GetControlPanelSsoUrlHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            new RecordingProvisioningProviderFactory(provider),
            new FixedAddressCaller("203.0.113.50"),
            NullLogger<GetControlPanelSsoUrlHandler>.Instance);

        var result = await handler.HandleAsync(new GetControlPanelSsoUrlQuery(seeded.ServiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        provider.SsoCallerIps.Should().Equal("203.0.113.50");
    }

    /// <summary>
    /// A terminated service keeps its reference but no longer has an account, so a sign-in link is
    /// refused with 400 <c>SERVICE_NOT_ACTIVE</c> instead of asking the panel.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetControlPanelSsoUrl_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/cpanel-sso");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
