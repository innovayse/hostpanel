using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/change-password</c>: setting a new password lets the account
/// sign in with it, a too-short password is refused by the validator, an unknown subject is
/// refused <c>IDENTITY_USER_NOT_FOUND</c>, where an external sign-on provider owns the accounts the write refuses with
/// its own code, a caller with only the Client role is refused, and the route is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class SetUserPasswordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Setting a new password lets the account sign in with it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LetsTheAccountSignInWithTheNewOneAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{subject}/change-password", new { password = "NewPassword123!" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = "NewPassword123!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A password under the minimum length is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesATooShortOneAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, _) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{subject}/change-password", new { password = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Setting a password for an unknown subject answers 404 with <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSubject_RefusesNotFoundAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/change-password", new { password = "NewPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

#if INNOVAYSE_SSO
    /// <summary>Where an external sign-on provider owns the accounts, setting a password refuses with its own code.</summary>
    [Fact]
    public async Task UnderSso_RefusesWithItsOwnCodeAsync()
    {
        var host = UsersTestSeeds.SsoModeHost(factory, knownAccount: null);
        var client = UsersTestSeeds.SsoAdminClient(host);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{Guid.NewGuid():N}/change-password", new { password = "NewPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_PROVISIONING_NOT_ALLOWED_SET_PASSWORD);
    }
#endif

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "set-user-password-forbidden");

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/change-password", new { password = "NewPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/change-password", new { password = "NewPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
