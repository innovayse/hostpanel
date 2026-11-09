using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/reset-password</c>: sending a reset link for a known account
/// succeeds, an unknown subject is refused <c>IDENTITY_USER_NOT_FOUND</c>, where an external sign-on provider owns the
/// accounts the write refuses with its own code once the identity read finds an account, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class SendPasswordResetTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Sending a reset link for a known account succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task KnownSubject_SucceedsAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, _) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.PostAsync($"/api/admin/users/{subject}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Sending a reset link for an unknown subject answers 404 with <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSubject_RefusesNotFoundAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

#if INNOVAYSE_SSO
    /// <summary>
    /// Where an external sign-on provider owns the accounts, issuing a password-reset link refuses with its own code,
    /// reached only once the identity read finds an account, so the fake is seeded with one.
    /// </summary>
    [Fact]
    public async Task UnderSso_RefusesWithItsOwnCodeAsync()
    {
        var subject = Guid.NewGuid().ToString("N");
        var host = UsersTestSeeds.SsoModeHost(
            factory, new IdentityAccount(subject, "sso-user@example.com", "Sso", "User"));
        var client = UsersTestSeeds.SsoAdminClient(host);

        var response = await client.PostAsync($"/api/admin/users/{subject}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_PROVISIONING_NOT_ALLOWED_ISSUE_PASSWORD_RESET);
    }
#endif

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "send-password-reset-forbidden");

        var response = await client.PostAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync(
            $"/api/admin/users/{UsersTestSeeds.NoSuchSubject}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
