using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// The account's contacts and the account holder's own details for the users of an account: a
/// user linked with the contacts permission reads and writes the account's contacts; the profile,
/// which carries the holder's sign-in details, and the account's users stay with the owner even for
/// a linked user holding every permission.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserAccountAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner reads the profile and the account's users.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerReadsTheProfileAndTheUsersAsync()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "linked-account-owner");

        var profile = await owner.Client.GetAsync("/api/clients/me");
        var users = await owner.Client.GetAsync("/api/me/users");

        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        users.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked with the contacts permission reads the account's contacts and adds one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserReadsAndAddsContactsAsync()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "linked-contact-permitted-owner");
        var first = await owner.Client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("OwnerAdded"));
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-contact-permitted", owner.ClientId, ClientPermission.ViewManageContacts);

        // A second contact with the first one's address would be refused, so this one names its own.
        var added = await user.PostAsJsonAsync(
            "/api/clients/me/contacts", ClientsTestSeeds.ContactBody("LinkedAdded", $"linked-{Guid.NewGuid():N}@example.test", true));
        var list = await user.GetAsync("/api/clients/me/contacts");

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await list.Content.ReadAsStringAsync();
        body.Should().Contain("OwnerAdded").And.Contain("LinkedAdded");
    }

    /// <summary>A user linked without the contacts permission is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "linked-contact-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-contact-denied", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/clients/me/contacts");

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A user permitted on another account does not see this account's contacts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountDoesNotSeeTheContactsAsync()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "linked-contact-foreign-owner");
        await owner.Client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("OwnersOnly"));
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-contact-foreign", otherClientId, ClientPermission.ViewManageContacts);

        var list = await user.GetAsync("/api/clients/me/contacts");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync()).Should().NotContain("OwnersOnly");
    }

    /// <summary>
    /// A linked user holding every permission still never reaches the owner's profile or the
    /// account's users: both answer <c>CLIENT_PROFILE_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithEveryPermissionNeverReachesTheOwnerOnlyActionsAsync()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "linked-owner-only-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-owner-only", owner.ClientId, ClientPermission.All);

        var profile = await user.GetAsync("/api/clients/me");
        var update = await user.PatchAsJsonAsync("/api/clients/me", new { firstName = "Taken" });
        var users = await user.GetAsync("/api/me/users");

        foreach (var response in new[] { profile, update, users })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }
    }
}
