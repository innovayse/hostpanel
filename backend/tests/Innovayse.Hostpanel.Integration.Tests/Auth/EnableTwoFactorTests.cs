using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Integration tests for the <c>EnableTwoFactor</c> use case: <c>POST /api/me/2fa/enable</c>.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EnableTwoFactorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Enabling with the code an authenticator app would show switches it on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnableWithTheRightCodeSwitchesItOn()
    {
        using var two = new TwoFactorHost(factory);
        var client = await AuthTestSeeds.NewAccountClientAsync(two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);

        var response = await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = two.CurrentCode(secret) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Enabling with a code that does not match the issued secret is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnableRefusesAWrongCode()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(factory.CreateClient());
        await AuthTestSeeds.StartSetupAsync(client);

        var response = await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.TWO_FACTOR_CODE_INVALID);
    }

    /// <summary>Once enabled, a login with just the password answers that a second factor is required.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LoginAfterEnablingAsksForTheSecondFactor()
    {
        using var two = new TwoFactorHost(factory);
        var client = two.CreateClient();
        var email = $"twofactor-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";
        (await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, firstName = "Two", lastName = "Factor" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var firstLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        using (var loginBody = JsonDocument.Parse(await firstLogin.Content.ReadAsStringAsync()))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", loginBody.RootElement.GetProperty("accessToken").GetString());
        }

        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = two.CurrentCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        secondLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await secondLogin.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("twoFactorRequired").GetBoolean().Should().BeTrue();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnableRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/2fa/enable", new { code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
