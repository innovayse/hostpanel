using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Change password, change package, credentials and the sign-in link reach the panel only for a
/// service that still holds its hosting account. A terminated service keeps its reference, and the
/// same reference may belong to a new live service, so the panel must not be asked about it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TerminatedServiceActionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Provisions a service and moves it to <paramref name="target"/> directly in the database.</summary>
    /// <param name="target">The status the service should end in.</param>
    /// <returns>The service id and the owner's token.</returns>
    private async Task<OwnedService> SeedAsync(ServiceStatus target)
    {
        var http = factory.CreateClient();
        var owned = await ProvisioningTestSeeds.SeedOwnedServiceAsync(factory, http, await factory.GetAdminTokenAsync());
        (await http.PostAsync($"/api/provisioning/{owned.ServiceId}/provision", null)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == owned.ServiceId);
        if (target == ServiceStatus.Suspended)
        {
            service.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
        }
        else if (target == ServiceStatus.Terminated)
        {
            service.Terminate(DateTimeOffset.UtcNow, "closed", hostingAlreadyUpdated: true);
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return owned;
    }

    /// <summary>Sends the four account-level requests for a service as staff, in a fixed order.</summary>
    /// <param name="http">The staff client.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The four responses.</returns>
    private static async Task<HttpResponseMessage[]> CallAllAsync(HttpClient http, int serviceId)
    {
        return
        [
            await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/change-password", new { newPassword = "N3wPassw0rd!" }),
            await http.PostAsJsonAsync($"/api/provisioning/{serviceId}/change-package", new { newPackage = "gold" }),
            await http.GetAsync($"/api/provisioning/{serviceId}/credentials"),
            await http.GetAsync($"/api/provisioning/{serviceId}/cpanel-sso"),
        ];
    }

    /// <summary>A terminated service is refused with 400 <c>SERVICE_NOT_ACTIVE</c> and the provider is never called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Terminated_AllFourRequestsAreRefusedWithoutReachingTheProviderAsync()
    {
        var owned = await SeedAsync(ServiceStatus.Terminated);
        var provider = new FakeProvisioningProvider();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        foreach (var response in await CallAllAsync(http, owned.ServiceId))
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        provider.AccountCalls.Should().Be(0);
    }

    /// <summary>An active and a suspended service still hold their account, so each request reaches the provider.</summary>
    /// <param name="target">The status of the service.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ServiceStatus.Active)]
    [InlineData(ServiceStatus.Suspended)]
    public async Task LiveAccount_AllFourRequestsReachTheProviderAsync(ServiceStatus target)
    {
        var owned = await SeedAsync(target);
        var provider = new FakeProvisioningProvider();
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        foreach (var response in await CallAllAsync(http, owned.ServiceId))
        {
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        provider.ChangePasswordCalls.Should().Be(1);
        provider.ChangePackageCalls.Should().Be(1);
        provider.CredentialsCalls.Should().Be(1);
        provider.SsoCalls.Should().Be(1);
    }

    /// <summary>
    /// A terminated service and a live one share a server and a reference. The former owner is refused
    /// with 400 <c>SERVICE_NOT_ACTIVE</c> and the provider receives no call, so nothing reaches the account
    /// that now belongs to the other service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FormerOwner_SharedReferenceWithALiveService_IsRefusedAndTheProviderIsNotCalledAsync()
    {
        var former = await SeedAsync(ServiceStatus.Terminated);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var old = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == former.ServiceId);
            var newOwner = Client.Create(
                $"subject-{Guid.NewGuid():N}", "Grace", "Hopper", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
            context.Clients.Add(newOwner);
            await context.SaveChangesAsync(CancellationToken.None);

            var live = ClientService.Create(newOwner.Id, old.ProductId, old.BillingCycle, DateTimeOffset.UtcNow);
            await ServicesTestSeeds.AddActivatedAsync(
                context, live, old.ProvisioningRef!, DateTimeOffset.UtcNow, old.ServerId);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var provider = new FakeProvisioningProvider();
        var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, provider);
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", former.OwnerToken);

        var response = await http.PostAsJsonAsync(
            $"/api/me/services/{former.ServiceId}/change-password", new { newPassword = "N3wPassw0rd!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        provider.AccountCalls.Should().Be(0);
    }
}
