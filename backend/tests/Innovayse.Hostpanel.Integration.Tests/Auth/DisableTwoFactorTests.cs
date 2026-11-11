using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// Integration tests for the <c>DisableTwoFactor</c> use case: <c>POST /api/me/2fa/disable</c>.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DisableTwoFactorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>The API host and the frozen clock its codes are computed from.</summary>
    private readonly TwoFactorHost _two = new(factory);

    /// <summary>Disabling with the current code switches it off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableWithTheRightCodeSwitchesItOff()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = _two.CurrentCode(secret), currentPassword = AuthTestSeeds.AccountPassword });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Switching it off ends every session issued before: the token that did it is refused on its
    /// next call.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableEndsTheSessionsIssuedBefore()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = _two.CurrentCode(secret), currentPassword = AuthTestSeeds.AccountPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync("/api/auth/email-verified");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The right code without the current password is refused, and the second factor stays on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableRefusesAWrongPassword()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var wrong = await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = _two.CurrentCode(secret), currentPassword = "Not-The-Password-1" });
        var missing = await client.PostAsJsonAsync("/api/me/2fa/disable", new { code = _two.CurrentCode(secret) });

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var body = JsonDocument.Parse(await wrong.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.CURRENT_PASSWORD_INVALID);
        }

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/me/2fa/setup", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "two-factor is still on");
    }

    /// <summary>The code that just enabled two-factor cannot also switch it off: an accepted code is good once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableRefusesTheCodeThatEnabledIt()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        var code = _two.CurrentCode(secret);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code, currentPassword = AuthTestSeeds.AccountPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.TWO_FACTOR_CODE_INVALID);
    }

    /// <summary>Ten wrong codes lock the account's codes: the right one is refused afterwards.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableRefusesTheRightCodeAfterTooManyWrongOnes()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var wrong = _two.CurrentCode(secret) == "000000" ? "111111" : "000000";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await client.PostAsJsonAsync(
                "/api/me/2fa/disable", new { code = wrong, currentPassword = AuthTestSeeds.AccountPassword });
        }

        var response = await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = _two.CurrentCode(secret), currentPassword = AuthTestSeeds.AccountPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Disabling with a code that does not match is refused, and the second factor stays on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableRefusesAWrongCode()
    {
        var client = await AuthTestSeeds.NewAccountClientAsync(_two.CreateClient());
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync(
            "/api/me/2fa/disable", new { code = "000000", currentPassword = AuthTestSeeds.AccountPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.TWO_FACTOR_CODE_INVALID);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisableRefusesAnonymous()
    {
        var response = await _two.CreateClient().PostAsJsonAsync("/api/me/2fa/disable", new { code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Disposes the clocked host.</summary>
    public void Dispose()
    {
        _two.Dispose();
    }
}
