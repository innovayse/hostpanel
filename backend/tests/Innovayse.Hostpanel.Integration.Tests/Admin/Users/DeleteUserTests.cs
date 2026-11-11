using System.Net;
using System.Net.Http.Headers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>DELETE /api/admin/users/{id}</c>: deleting an unknown subject still succeeds (this
/// deployment's own delete is idempotent), an admin cannot delete their own account or the last
/// active administrator, another administrator can be deleted, where an external sign-on provider owns the accounts the delete refuses
/// with its own code, a caller with only the Client role is refused, and the route is closed to
/// a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class DeleteUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Deleting an unknown subject still answers success: this deployment's own delete is
    /// idempotent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSubject_StillSucceedsAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

#if INNOVAYSE_SSO
    /// <summary>Where an external sign-on provider owns the accounts, deleting one refuses with the delete-account code.</summary>
    [Fact]
    public async Task UnderSso_RefusesWithItsOwnCodeAsync()
    {
        var host = UsersTestSeeds.SsoModeHost(factory, knownAccount: null);
        var client = UsersTestSeeds.SsoAdminClient(host);

        var response = await client.DeleteAsync($"/api/admin/users/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT);
    }
#endif

    /// <summary>An admin cannot delete their own account; it stays live.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnAccount_IsRefusedAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var self = await SubjectOfAsync(factory, "admin-seed@innovayse.test");

        var response = await client.DeleteAsync($"/api/admin/users/{self}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_CANNOT_DELETE_SELF);
        (await client.GetAsync($"/api/admin/users/{self}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The last active administrator cannot be deleted: a caller whose token still carries Admin
    /// after the role was taken from them cannot remove the one account that holds it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LastActiveAdmin_IsRefusedAsync()
    {
        await using var isolated = new IntegrationTestFactory();
        await isolated.InitializeAsync();
        var callerEmail = $"former-admin-{Guid.NewGuid():N}@example.com";
        var lastEmail = $"last-admin-{Guid.NewGuid():N}@example.com";
        await CreateAdminAsync(isolated, callerEmail);
        var last = await CreateAdminAsync(isolated, lastEmail);
        var token = await isolated.GetClientTokenAsync(callerEmail, UsersTestSeeds.TestPassword);
        await RevokeAdminAsync(isolated, callerEmail);
        var client = isolated.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/admin/users/{last}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_LAST_ADMIN);
    }

    /// <summary>An administrator who is not the last one can be deleted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherAdmin_CanBeDeletedAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var other = await CreateAdminAsync(factory, $"other-admin-{Guid.NewGuid():N}@example.com");

        var response = await client.DeleteAsync($"/api/admin/users/{other}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "delete-user-forbidden");

        var response = await client.DeleteAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Creates a local account holding the Admin role.</summary>
    /// <param name="host">The host whose database receives the account.</param>
    /// <param name="email">The account's address.</param>
    /// <returns>The account's subject.</returns>
    private static async Task<string> CreateAdminAsync(IntegrationTestFactory host, string email)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = "Ada",
            LastName = "Admin",
            EmailConfirmed = true,
        };
        (await users.CreateAsync(user, UsersTestSeeds.TestPassword)).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, Roles.Admin)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    /// <summary>Takes the Admin role from a local account.</summary>
    /// <param name="host">The host whose database holds the account.</param>
    /// <param name="email">The account's address.</param>
    /// <returns>A task that completes once the role is gone.</returns>
    private static async Task RevokeAdminAsync(IntegrationTestFactory host, string email)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        (await users.RemoveFromRoleAsync(user!, Roles.Admin)).Succeeded.Should().BeTrue();
    }

    /// <summary>Reads the subject of a local account.</summary>
    /// <param name="host">The host whose database holds the account.</param>
    /// <param name="email">The account's address.</param>
    /// <returns>The account's subject.</returns>
    private static async Task<string> SubjectOfAsync(IntegrationTestFactory host, string email)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }
}
