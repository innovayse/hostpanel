using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Integration tests for the <c>StartTwoFactorSetup</c> use case: <c>POST /api/me/2fa/setup</c>,
/// issuing a fresh TOTP secret and the URI an authenticator app scans.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartTwoFactorSetupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A signed-in account is issued a secret and an <c>otpauth://</c> URI.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_SignedInAccount_IssuesASecret()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(factory.CreateClient());

        var secret = await AuthTestSeeds.StartSetupAsync(client);

        secret.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// A setup on an account that already has two-factor on is refused, and the secret the account
    /// signs in with is left as it was: a code from it still switches two-factor off.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_WhileEnabled_IsRefusedAndKeepsTheActiveSecret()
    {
        using var two = new TwoFactorHost(factory);
        var client = await AuthTestSeeds.NewAccountClientAsync(two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsync("/api/me/2fa/setup", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.TWO_FACTOR_ALREADY_ENABLED);
        }

        (await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = two.CurrentCode(secret), currentPassword = AuthTestSeeds.AccountPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A secret that was issued but never confirmed is not the one sign-in asks about: the account
    /// still signs in with its password alone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Setup_WithoutConfirming_LeavesSignInUnchanged()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(factory.CreateClient());
        await AuthTestSeeds.StartSetupAsync(client);
        var email = new JwtSecurityTokenHandler()
            .ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter)
            .Claims.First(c => c.Type == "email").Value;

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = AuthTestSeeds.AccountPassword });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("accessToken", out _).Should().BeTrue();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetupRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/me/2fa/setup", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
