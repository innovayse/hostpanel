using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>SuspendServiceHandler</c>, reached through the admin
/// <c>POST /api/services/{id}/suspend</c> and the staff <c>POST /api/provisioning/{id}/suspend</c> actions.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SuspendServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Suspending a service that does not exist answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_OnAnUnknownService_Returns404()
    {
        var client = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/suspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>An admin may suspend any client's service: the admin surface is not ownership-scoped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminSuspend_AnyClientsService_StillSucceeds()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "suspend-admin-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.FirstOrDefaultAsync(
                s => s.Id == serviceId, CancellationToken.None);
            service!.Activate("panel-ref-admin-test", service.CreatedAt, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.PostAsync($"/api/services/{serviceId}/suspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A staff suspension through the admin route is recorded as <see cref="SuspensionSource.Manual"/>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminSuspend_RecordsTheSuspensionAsManualAsync()
    {
        var (_, ownerClientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "suspend-source-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, ownerClientId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var seeded = await context.ClientServices.FirstOrDefaultAsync(
                s => s.Id == serviceId, CancellationToken.None);
            seeded!.Activate("panel-ref-source-test", seeded.CreatedAt, DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var response = await admin.PostAsync($"/api/services/{serviceId}/suspend", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await verifyContext.ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
        reloaded.SuspensionSource.Should().Be(SuspensionSource.Manual);
    }

    /// <summary>Suspending without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/suspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client calling the admin suspend route is refused: the policy is admin only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_Returns403_AsPlainClientAsync()
    {
        var (client, _) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "suspend-forbidden");

        var response = await client.PostAsync($"/api/services/{ServicesTestSeeds.NoSuchServiceId}/suspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A service that is not Active is refused with 400 <c>SERVICE_NOT_ACTIVE</c> before the panel is
    /// asked: a refusing panel would otherwise have answered 502.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_NotActive_Returns400BeforeThePanelIsAskedAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        (await setupHttp.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment" }))
            .EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Again" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>A pending service is refused with 400 <c>SERVICE_NOT_ACTIVE</c>, not a server error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_PendingService_Returns400ServiceNotActiveAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/services/{serviceId}/suspend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
    }

    /// <summary>Staff can suspend a provisioned service through the provisioning route; it is recorded as manual.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProvisioningSuspend_ProvisionedService_Returns204AsManualAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment", source = "Overdue" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        stored.Status.Should().Be(ServiceStatus.Suspended);
        stored.SuspensionSource.Should().Be(SuspensionSource.Manual);
    }

    /// <summary>
    /// A provider that refuses to suspend answers 502 with <c>SERVICE_SUSPEND_UPSTREAM_FAILED</c>, and the
    /// service stays Active on the platform.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_ProviderRefuses_Returns502AndLeavesTheServiceActiveAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_SUSPEND_UPSTREAM_FAILED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// One suspension asks the panel exactly once: the handler suspends the account itself, so the
    /// domain events it publishes must not ask the panel a second time.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Suspend_AsksThePanelExactlyOnceAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        var provider = new FakeProvisioningProvider();
        var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, provider);
        var http = await ProvisioningTestSeeds.AdminClientOnAsync(factory, host);
        HttpResponseMessage? response = null;

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) =>
            {
                response = await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/suspend", new { reason = "Non-payment" });
            });

        response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        provider.SuspendCalls.Should().Be(1);
    }

    /// <summary>Suspending through the provisioning route without authentication is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProvisioningSuspend_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync(
            $"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/suspend", new { reason = "Non-payment" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
