using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for the admin <c>GET /api/provisioning/{id}/panel-login</c> action: how a person signs in to a panel by hand,
/// for a panel that cannot issue a single sign on link. The answer never carries a password.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPanelLoginDetailsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The admin reads the panel's sign in page, the user name, the domain and the server, and a stored password is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_ReturnsTheDetailsWithoutAPasswordAsync()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider());

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(raw);
        body.RootElement.GetProperty("username").GetString().Should().Be("fake-user");
        body.RootElement.GetProperty("domain").GetString().Should().Be("fake.example.com");
        body.RootElement.GetProperty("serverAddress").GetString().Should().Be("1.2.3.4");
        body.RootElement.GetProperty("loginUrl").GetString().Should().Be("https://panel.fake.example.com");
        raw.Should().NotContain("fake-pass");
        body.RootElement.TryGetProperty("password", out _).Should().BeFalse();
    }

    /// <summary>A panel whose plugin has no login capability answers <c>PANEL_OPERATION_NOT_SUPPORTED</c>, so the caller has nothing to show.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_PluginWithoutTheLoginCapability_Returns409WithItsCodeAsync()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var refusing = new FakeProvisioningProvider
        {
            FailWith = new DomainRuleException(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The panel plugin does not declare the login capability."),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, refusing);

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
    }

    /// <summary>An id no service takes answers 404 with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_UnknownService_Returns404WithServiceNotFoundAsync()
    {
        var http = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await http.GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetPanelLoginDetails_Returns401_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/panel-login");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
