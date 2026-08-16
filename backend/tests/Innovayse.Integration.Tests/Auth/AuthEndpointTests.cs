namespace Innovayse.Integration.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

/// <summary>Integration tests for POST /api/auth/* endpoints.</summary>
public sealed class AuthEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Register with valid data returns 200 and the new user's id.
    /// </summary>
    /// <remarks>
    /// It used to return an access token as well, and this test asserted on one. Signing
    /// the caller in as a side effect of creating an account is the part that went away:
    /// registration creates the account, and a token comes from /api/auth/login like it
    /// does for everyone else. The login case below is what covers the token.
    /// </remarks>
    [Fact]
    public async Task Register_WithValidData_Returns200WithUserIdAsync()
    {
        var client = factory.CreateClient();
        var body = new
        {
            email = $"test-{Guid.NewGuid()}@example.com",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("userId");
    }

    /// <summary>Login with valid credentials returns 200 with access token.</summary>
    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithAccessTokenAsync()
    {
        var client = factory.CreateClient();
        var email = $"login-test-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("accessToken");
    }

    /// <summary>Login with wrong password returns non-200 status.</summary>
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsErrorAsync()
    {
        var client = factory.CreateClient();
        var email = $"wrong-pwd-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    // There was a Logout_Returns204Async here, asserting POST /api/auth/logout returns
    // 204. That endpoint does not exist in the mode this suite runs. It comes from
    // Innovayse.Auth's MapInnovayseAuth, which is only wired up under Auth:Mode=sso —
    // these tests run local, where the session is a bearer JWT with nothing to
    // invalidate server-side. Nothing calls it either: the client app signs out through
    // its own Nuxt route, /api/portal/auth/logout. The test asserted a 404 was a 204.
}
