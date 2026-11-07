using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for <c>POST /api/provisioning/{id}/provision</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProvisionServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can provision a pending service.</summary>
    [Fact]
    public async Task Provision_AdminCanProvisionServiceAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Client role calling /api/provisioning/{id}/provision receives 403 Forbidden.</summary>
    [Fact]
    public async Task Provision_ClientForbiddenAsync()
    {
        var http = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();

        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, http, adminToken);

        var email = $"prov-forbidden-{Guid.NewGuid():N}@example.com";
        http.DefaultRequestHeaders.Authorization = null;

        var registerResponse = await http.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Pass@123!",
            firstName = "Forbidden",
            lastName = "Client"
        });
        registerResponse.EnsureSuccessStatusCode();

        var clientToken = await factory.GetClientTokenAsync(email, "Pass@123!");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Provisioning an id no service takes answers 404 with <c>SERVICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Provision_UnknownService_Returns404WithServiceNotFound()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.PostAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// A provider that refuses to provision answers 502 with <c>PROVISIONING_UPSTREAM_FAILED</c>,
    /// and no provider detail reaches the body.
    /// </summary>
    [Fact]
    public async Task Provision_ProviderRefuses_Returns502WithProvisioningUpstreamFailed()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);

        var failing = new FakeProvisioningProvider { ShouldFail = true };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, failing);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("fake provider");
    }

    /// <summary>Provisioning without authentication is refused.</summary>
    [Fact]
    public async Task Provision_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.PostAsync($"/api/provisioning/{ProvisioningTestSeeds.NoSuchServiceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A pending service with no domain or username is refused with
    /// <c>SERVICE_HOSTING_DETAILS_MISSING</c> instead of a made-up account being created.
    /// </summary>
    [Fact]
    public async Task Provision_WithoutHostingDetails_RefusesHostingDetailsMissing()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result>(new ProvisionServiceCommand(service.Id), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_HOSTING_DETAILS_MISSING);
    }

    /// <summary>
    /// A service whose product names no hosting package is refused with
    /// <c>PRODUCT_PACKAGE_MISSING</c> instead of a placeholder package being sent to the panel.
    /// </summary>
    [Fact]
    public async Task Provision_ProductWithoutPackage_RefusesPackageMissing()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, _) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ProductGroup.Create($"no-package-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, "No package", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        service.AssignHostingAccount("example.com", "jdoe", "Passw0rd!");
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result>(new ProvisionServiceCommand(service.Id), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_PACKAGE_MISSING);
    }

    /// <summary>
    /// A retry that meets the account an earlier attempt created (same username and domain)
    /// adopts it and activates the service instead of failing again.
    /// </summary>
    [Fact]
    public async Task Provision_ExistingAccountOfTheService_AdoptsItAndActivates()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider
        {
            ProvisionAnswer = ProvisioningResult.AlreadyExists($"prov{serviceId}", $"prov{serviceId}.example.com"),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await ReloadAsync(serviceId);
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.ProvisioningRef.Should().Be($"prov{serviceId}");
    }

    /// <summary>
    /// An existing account under the username that belongs to another domain is not adopted: the
    /// service stays Pending and the answer is 409 <c>PROVISIONING_ACCOUNT_CONFLICT</c>.
    /// </summary>
    [Fact]
    public async Task Provision_ExistingAccountOfSomebodyElse_Returns409AccountConflict()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider
        {
            ProvisionAnswer = ProvisioningResult.AlreadyExists($"prov{serviceId}", "someone-else.example.com"),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PROVISIONING_ACCOUNT_CONFLICT);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>
    /// A provider that answers success without an account reference is refused with 502, and the
    /// service is not activated with a reference no later operation could use.
    /// </summary>
    [Fact]
    public async Task Provision_SuccessWithoutReference_Returns502AndStaysPending()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider
        {
            ProvisionAnswer = new ProvisioningResult(ProvisioningOutcome.Created, null, null, null),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>
    /// A password generated for a service that had none is saved before the panel is called, so a
    /// refused attempt keeps it and the retry sends the same one.
    /// </summary>
    [Fact]
    public async Task Provision_GeneratedPassword_IsSavedBeforeTheProviderIsCalled()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ClientServices
                .Where(s => s.Id == serviceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Password, (string?)null));
        }

        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider { ShouldFail = true });

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var stored = await ReloadAsync(serviceId);
        stored.Status.Should().Be(ServiceStatus.Pending);
        stored.Password.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// A provider that stops the call because the stored username or domain is one the panel can never
    /// accept answers 409 with that code, and the service stays Pending.
    /// </summary>
    /// <param name="code">The provider's coded refusal.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HostpanelErrorCodes.CPANEL_USERNAME_INVALID)]
    [InlineData(HostpanelErrorCodes.CPANEL_DOMAIN_INVALID)]
    public async Task Provision_ProviderRejectsTheStoredDetails_Returns409WithItsCodeAsync(string code)
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider { FailWith = new DomainRuleException(code, "stored details are not valid") };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(code);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>
    /// A provider that cannot tell whether the account was created answers 503 with its own
    /// <c>PROVISIONING_OUTCOME_UNKNOWN</c>, distinct from a refusal, and the service stays Pending so
    /// the caller can retry.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ProviderOutcomeUnknown_Returns503WithItsOwnCodeAndStaysPendingAsync()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider
        {
            FailWith = new DomainRuleException(HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN, "the answer was lost"),
        };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("the answer was lost");
        provider.ProvisionCalls.Should().Be(1);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>
    /// An unexpected provider failure is still a 502 <c>PROVISIONING_UPSTREAM_FAILED</c>: only the
    /// provider's own unknown-outcome signal changes the answer.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ProviderThrowsAnythingElse_StillReturns502Async()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var provider = new FakeProvisioningProvider { FailWith = new InvalidOperationException("boom") };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED);
    }

    /// <summary>
    /// The first billing period starts when provisioning succeeds, not on the order date: a service
    /// ordered months ago renews one cycle after its activation.
    /// </summary>
    [Fact]
    public async Task Provision_AnchorsTheRenewalOnTheActivationMoment()
    {
        var setupHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, await factory.GetAdminTokenAsync());
        var orderedAt = DateTimeOffset.UtcNow.AddMonths(-3);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ClientServices
                .Where(s => s.Id == serviceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, orderedAt));
        }

        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, new FakeProvisioningProvider());
        var before = DateTimeOffset.UtcNow;

        var response = await http.PostAsync($"/api/provisioning/{serviceId}/provision", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await ReloadAsync(serviceId);
        stored.NextRenewalAt.Should().NotBeNull();
        stored.NextRenewalAt!.Value.Should().BeOnOrAfter(before.AddMonths(1).AddSeconds(-1));
        stored.NextRenewalAt.Value.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddMonths(1));
    }

    /// <summary>Reads a service back from the database in a fresh scope.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The stored service.</returns>
    private async Task<ClientService> ReloadAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
    }
}
