using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for the admin <c>POST /api/provisioning/{id}/change-package</c> action.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChangePackageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can change the hosting package of a provisioned service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangePackage_AdminCanChangeAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{serviceId}/change-package", new { newPackage = "premium" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Changing the package of a service with no assigned server answers 400 with <c>SERVICE_NO_SERVER_ASSIGNED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangePackage_ServiceHasNoServer_Returns400WithServiceNoServerAssigned()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        await ProvisioningTestSeeds.ClearAssignedServerAsync(factory, serviceId);

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{serviceId}/change-package", new { newPackage = "premium" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
    }

    /// <summary>Changing the package for an id no service takes answers 404 with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangePackage_UnknownService_Returns404WithServiceNotFound()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/change-package", new { newPackage = "premium" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// A provider that refuses to change the package answers 502 with
    /// <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangePackage_ProviderRefuses_Returns502WithServicePackageUpstreamFailed()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();

        var failing = new FakeProvisioningProvider { ShouldFail = true };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, failing);

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{serviceId}/change-package", new { newPackage = "premium" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED);
    }

    /// <summary>Changing the package without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangePackage_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/change-package", new { newPackage = "premium" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
