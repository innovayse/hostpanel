using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>TerminateServiceHandler</c>, reached through the admin
/// <c>POST /api/services/{id}/terminate</c> and the staff <c>POST /api/provisioning/{id}/terminate</c> actions.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TerminateServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Terminating a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_OnAnUnknownService_Returns404()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/terminate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>An admin can terminate an existing service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_OnAnExistingService_Returns204()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "terminate-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/services/{serviceId}/terminate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Terminating without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/terminate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling the admin terminate route is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "terminate-forbidden");

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/terminate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A terminated service is refused with 400 <c>SERVICE_TERMINATE_NOT_ALLOWED</c> before the panel is
    /// asked: a refusing panel would otherwise have answered 502.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_AlreadyTerminated_Returns400BeforeThePanelIsAskedAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        (await setupHttp.PostAsJsonAsync($"/api/provisioning/{serviceId}/terminate", new { reason = "Cancelled" }))
            .EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/terminate", new { reason = "Again" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_TERMINATE_NOT_ALLOWED);
    }

    /// <summary>Terminating a provisioned service with no server assigned still succeeds: there is nowhere upstream to tell.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProvisioningTerminate_ProvisionedServiceWithNoServer_Returns204Async()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        await ProvisioningTestSeeds.ClearAssignedServerAsync(factory, serviceId);

        var response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/terminate", new { reason = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A provider that refuses to terminate answers 502 with <c>SERVICE_TERMINATE_UPSTREAM_FAILED</c>, and
    /// the service is not marked terminated on the platform.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminate_ProviderRefuses_Returns502AndLeavesTheServiceAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/terminate", new { reason = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_TERMINATE_UPSTREAM_FAILED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Active);
    }
}
