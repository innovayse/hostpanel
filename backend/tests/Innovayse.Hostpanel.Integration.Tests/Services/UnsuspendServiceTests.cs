using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>UnsuspendServiceHandler</c>, reached through the admin
/// <c>POST /api/services/{id}/unsuspend</c> and the staff <c>POST /api/provisioning/{id}/unsuspend</c> actions.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UnsuspendServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Unsuspending a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsuspend_OnAnUnknownService_Returns404()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/unsuspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>An admin can unsuspend a suspended service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsuspend_OnASuspendedService_Returns204()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "unsuspend-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.FirstOrDefaultAsync(
                s => s.Id == serviceId, CancellationToken.None);
            service!.Activate("panel-ref-unsuspend", service.CreatedAt, DateTimeOffset.UtcNow);
            service.Suspend(DateTimeOffset.UtcNow, "test");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.PostAsync($"/api/services/{serviceId}/unsuspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Unsuspending without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsuspend_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/unsuspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An Active service is refused with 400 <c>SERVICE_NOT_SUSPENDED</c> before the panel is asked: a
    /// refusing panel would otherwise have answered 502.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsuspend_NotSuspended_Returns400BeforeThePanelIsAskedAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/unsuspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_SUSPENDED);
    }

    /// <summary>Staff can unsuspend a suspended, provisioned service through the provisioning route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProvisioningUnsuspend_SuspendedService_Returns204Async()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment" }))
            .EnsureSuccessStatusCode();

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/unsuspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A provider that refuses to unsuspend answers 502 with <c>SERVICE_UNSUSPEND_UPSTREAM_FAILED</c>, and
    /// the service stays Suspended on the platform.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Unsuspend_ProviderRefuses_Returns502AndLeavesTheServiceSuspendedAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        (await setupHttp.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment" }))
            .EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/unsuspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_UNSUSPEND_UPSTREAM_FAILED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Suspended);
    }
}
