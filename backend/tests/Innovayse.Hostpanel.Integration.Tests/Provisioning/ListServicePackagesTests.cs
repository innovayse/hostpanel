using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Provisioning.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for the admin <c>GET /api/provisioning/{id}/packages</c> action: the names the server's panel offers, the code
/// of a panel that cannot list, a failing provider, an unknown service and a caller with no session.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServicePackagesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin gets the package names the provider lists, in its order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_AdminGetsTheNamesAsync()
    {
        var serviceId = await ProvisionedServiceAsync();
        var provider = new FakeProvisioningProvider();
        provider.Packages.Clear();
        provider.Packages.AddRange(["Starter", "Business", "Pro"]);
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ServicePackagesDto>())!.Names.Should().Equal("Starter", "Business", "Pro");
        provider.ListPackagesCalls.Should().Be(1);
    }

    /// <summary>A provider that cannot list answers 409 with <c>PANEL_OPERATION_NOT_SUPPORTED</c>, so the screen asks for the name by hand.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_ProviderCannotList_Returns409Async()
    {
        var serviceId = await ProvisionedServiceAsync();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { PackagesNotSupported = true });

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
    }

    /// <summary>A provider that fails answers 502 with <c>SERVICE_PACKAGES_UPSTREAM_FAILED</c> and no provider text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_ProviderFails_Returns502Async()
    {
        var serviceId = await ProvisionedServiceAsync();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PACKAGES_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("fake provider refused");
    }

    /// <summary>A service with no assigned server answers 400 with <c>SERVICE_NO_SERVER_ASSIGNED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_ServiceHasNoServer_Returns400Async()
    {
        var serviceId = await ProvisionedServiceAsync();
        await ProvisioningTestSeeds.ClearAssignedServerAsync(factory, serviceId);
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.GetAsync($"/api/provisioning/{serviceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
    }

    /// <summary>An id no service takes answers 404 with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_UnknownService_Returns404Async()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Listing without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPackages_Returns401_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Seeds a service and provisions it through the default stub provider.</summary>
    /// <returns>The service id.</returns>
    private async Task<int> ProvisionedServiceAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        return serviceId;
    }
}
