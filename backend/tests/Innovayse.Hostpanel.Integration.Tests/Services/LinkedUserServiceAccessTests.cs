using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>GET /api/me/services</c> and <c>GET /api/me/services/{id}/cancellation-status</c> for the users
/// of an account: the owner and a user linked with the services permission see the account's
/// services; a user without the permission is refused; a user of another account gets 404 for an
/// id that is not theirs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserServiceAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner lists their own service and reads its cancellation status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerSeesTheServiceAsync()
    {
        var (owner, serviceId) = await OwnerWithServiceAsync("linked-service-owner");

        var list = await owner.Client.GetAsync("/api/me/services");
        var status = await owner.Client.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(serviceId);
        status.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked with the services permission lists the account's service and reads its cancellation status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserSeesTheServiceAsync()
    {
        var (owner, serviceId) = await OwnerWithServiceAsync("linked-service-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-service-permitted", owner.ClientId, ClientPermission.ViewProductsServices);

        var list = await user.GetAsync("/api/me/services");
        var status = await user.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(serviceId);
        status.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked without the services permission is refused both routes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, serviceId) = await OwnerWithServiceAsync("linked-service-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-service-denied", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/services");
        var status = await user.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        foreach (var response in new[] { list, status })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }
    }

    /// <summary>
    /// A user permitted on another account never sees this account's service: the list is theirs
    /// alone and the id answers <c>SERVICE_NOT_FOUND</c>, like an id naming no service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountAnswersNotFoundAsync()
    {
        var (_, serviceId) = await OwnerWithServiceAsync("linked-service-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-service-foreign", otherClientId, ClientPermission.ViewProductsServices);

        var list = await user.GetAsync("/api/me/services");
        var status = await user.GetAsync($"/api/me/services/{serviceId}/cancellation-status");

        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(serviceId);
        status.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(status, HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Registers an account owner and seeds a service on the account.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The signed-in owner and the service id.</returns>
    private async Task<(SignedInClient Owner, int ServiceId)> OwnerWithServiceAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, owner.ClientId);
        return (owner, serviceId);
    }
}
