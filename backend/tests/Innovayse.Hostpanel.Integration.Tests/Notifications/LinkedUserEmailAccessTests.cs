using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// <c>GET /api/me/emails</c> for the users of an account: the owner and a user linked with the
/// emails permission read what was sent to the account; a user without the permission is refused;
/// a user of another account never sees it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserEmailAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner reads the email sent to the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerSeesTheEmailAsync()
    {
        var (owner, subject) = await OwnerWithEmailAsync("linked-email-owner");

        var list = await owner.Client.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync()).Should().Contain(subject);
    }

    /// <summary>A user linked with the emails permission reads the email sent to the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserSeesTheEmailAsync()
    {
        var (owner, subject) = await OwnerWithEmailAsync("linked-email-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-email-permitted", owner.ClientId, ClientPermission.ViewEmails);

        var list = await user.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync()).Should().Contain(subject);
    }

    /// <summary>A user linked without the emails permission is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, _) = await OwnerWithEmailAsync("linked-email-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-email-denied", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A user permitted on another account does not see the email sent to this account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountDoesNotSeeTheEmailAsync()
    {
        var (_, subject) = await OwnerWithEmailAsync("linked-email-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-email-foreign", otherClientId, ClientPermission.ViewEmails);

        var list = await user.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync()).Should().NotContain(subject);
    }

    /// <summary>Registers an account owner and records an email sent to the account's address.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The signed-in owner and the subject of the recorded email.</returns>
    private async Task<(SignedInClient Owner, string Subject)> OwnerWithEmailAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var subject = $"Linked access {Guid.NewGuid():N}";
        await NotificationsTestSeeds.SeedLogAsync(factory, owner.Email, subject);
        return (owner, subject);
    }
}
