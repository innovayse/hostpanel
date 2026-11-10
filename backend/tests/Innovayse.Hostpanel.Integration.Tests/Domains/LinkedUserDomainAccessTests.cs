using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <c>GET /api/me/domains</c> and <c>GET /api/me/domains/{id}</c> for the users of an account: the
/// owner and a user linked with the domains permission see the account's domains; a user without
/// the permission is refused; a user of another account gets 404 for an id that is not theirs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserDomainAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner lists and reads their own domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerSeesTheDomainAsync()
    {
        var (owner, domainId) = await OwnerWithDomainAsync("linked-domain-owner");

        var list = await owner.Client.GetAsync("/api/me/domains");
        var read = await owner.Client.GetAsync($"/api/me/domains/{domainId}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(domainId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked with the domains permission lists and reads the account's domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserSeesTheDomainAsync()
    {
        var (owner, domainId) = await OwnerWithDomainAsync("linked-domain-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-domain-permitted", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/domains");
        var read = await user.GetAsync($"/api/me/domains/{domainId}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(domainId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked without the domains permission is refused both routes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, domainId) = await OwnerWithDomainAsync("linked-domain-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-domain-denied", owner.ClientId, ClientPermission.ViewEmails);

        var list = await user.GetAsync("/api/me/domains");
        var read = await user.GetAsync($"/api/me/domains/{domainId}");

        foreach (var response in new[] { list, read })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }
    }

    /// <summary>
    /// A user permitted on another account never sees this account's domain: the list is theirs
    /// alone and the id answers <c>DOMAIN_NOT_FOUND</c>, like an id naming no domain.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountAnswersNotFoundAsync()
    {
        var (_, domainId) = await OwnerWithDomainAsync("linked-domain-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-domain-foreign", otherClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/domains");
        var read = await user.GetAsync($"/api/me/domains/{domainId}");

        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(domainId);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(read, HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>Registers an account owner and registers a domain on the account as staff.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The signed-in owner and the domain id.</returns>
    private async Task<(SignedInClient Owner, int DomainId)> OwnerWithDomainAsync(string label)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, owner.ClientId);
        return (owner, domainId);
    }
}
