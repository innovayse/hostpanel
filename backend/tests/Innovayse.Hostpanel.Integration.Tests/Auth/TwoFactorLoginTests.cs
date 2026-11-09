using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/2fa-login</c>: the pending token that the password step hands out is the only
/// way in, so a token that was forged, altered, expired, replayed or guessed against too often is
/// refused even when the one-time code is correct.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TwoFactorLoginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>A password that satisfies the local password policy.</summary>
    private const string Password = "Password123!";

    /// <summary>The API host and the frozen clock its codes are computed from.</summary>
    private readonly TwoFactorHost _two = new(factory);

    /// <summary>A forged token in the retired base64 format is refused even with the right code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForgedBase64Token_IsRefusedEvenWithTheRightCode()
    {
        var account = await NewTwoFactorAccountAsync();
        var forged = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"2fa:{account.UserId}:{DateTime.UtcNow.Ticks}"));

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = forged, code = _two.CurrentCode(account.Secret) });

        await AssertRefusalAsync(response, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A genuine token with one character changed is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TamperedToken_IsRefused()
    {
        var account = await NewTwoFactorAccountAsync();
        var token = await PendingTokenAsync(account);
        var middle = token.Length / 2;
        var tampered = token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = tampered, code = _two.CurrentCode(account.Secret) });

        await AssertRefusalAsync(response, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>
    /// A token signed with the host's own key ring but already past its lifetime is refused. It is
    /// issued by a second service instance with a negative lifetime, so no clock has to be waited out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExpiredToken_IsRefused()
    {
        var account = await NewTwoFactorAccountAsync();
        var born = new TwoFactorChallengeService(
            _two.Services.GetRequiredService<IDataProtectionProvider>(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TwoFactorChallengeOptions { LifetimeSeconds = -60 }));

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login",
            new { pendingToken = born.Issue(account.UserId), code = _two.CurrentCode(account.Secret) });

        await AssertRefusalAsync(response, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A genuine token and the right code sign in and return an access token.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidTokenAndCorrectCode_SignsIn()
    {
        var account = await NewTwoFactorAccountAsync();
        var token = await PendingTokenAsync(account);

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = token, code = _two.CurrentCode(account.Secret) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>A token that already signed in once cannot be replayed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TokenAlreadyUsed_IsRefused()
    {
        var account = await NewTwoFactorAccountAsync();
        var token = await PendingTokenAsync(account);
        var code = _two.CurrentCode(account.Secret);
        (await Client().PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = token, code }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await Client().PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = token, code });

        await AssertRefusalAsync(replay, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>
    /// Five wrong codes are answered as wrong codes; after that the challenge is dead and even the
    /// correct code is refused, so the six digits cannot be brute forced against one token.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TooManyWrongCodes_KillTheChallenge()
    {
        var account = await NewTwoFactorAccountAsync();
        var token = await PendingTokenAsync(account);
        var wrong = _two.CurrentCode(account.Secret) == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var miss = await Client().PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = token, code = wrong });
            await AssertRefusalAsync(miss, HostpanelErrorCodes.TWO_FACTOR_CODE_UNAUTHORIZED);
        }

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = token, code = _two.CurrentCode(account.Secret) });

        await AssertRefusalAsync(response, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A dead challenge does not stop the same person starting a new one with the password.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeadChallenge_DoesNotBlockANewSignIn()
    {
        var account = await NewTwoFactorAccountAsync();
        var dead = await PendingTokenAsync(account);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Client().PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = dead, code = "000000" });
        }

        var fresh = await PendingTokenAsync(account);
        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = fresh, code = _two.CurrentCode(account.Secret) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A code that already signed one challenge in is refused on a second one in the same time window.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CodeAcceptedOnce_IsRefusedOnAnotherChallenge()
    {
        var account = await NewTwoFactorAccountAsync();
        var code = _two.CurrentCode(account.Secret);
        (await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = await PendingTokenAsync(account), code }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login", new { pendingToken = await PendingTokenAsync(account), code });

        await AssertRefusalAsync(replay, HostpanelErrorCodes.TWO_FACTOR_CODE_UNAUTHORIZED);
    }

    /// <summary>
    /// Wrong codes are counted per account across challenges: after ten of them, spread over several
    /// pending tokens, even a fresh token with the right code is refused.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongCodesAcrossChallenges_LockTheAccount()
    {
        var account = await NewTwoFactorAccountAsync();
        for (var challenge = 0; challenge < 2; challenge++)
        {
            var token = await PendingTokenAsync(account);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Client().PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = token, code = "000000" });
            }
        }

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login",
            new { pendingToken = await PendingTokenAsync(account), code = _two.CurrentCode(account.Secret) });

        await AssertRefusalAsync(response, HostpanelErrorCodes.TWO_FACTOR_CODE_UNAUTHORIZED);
    }

    /// <summary>
    /// A sign-in finished at the second factor carries the account's real email-verified state, as the
    /// password-only sign-in does; the freshly registered account here has not confirmed its address.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SignInAtTheSecondFactor_CarriesTheEmailVerifiedState()
    {
        var account = await NewTwoFactorAccountAsync();

        var response = await Client().PostAsJsonAsync(
            "/api/auth/2fa-login",
            new { pendingToken = await PendingTokenAsync(account), code = _two.CurrentCode(account.Secret) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = body.RootElement.GetProperty("accessToken").GetString()!.Split('.')[1]
            .Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
        claims.RootElement.GetProperty("email_verified").GetString().Should().Be("false");
    }

    /// <summary>A client that asks for Russian, like the other auth refusals.</summary>
    /// <returns>An unauthenticated client with <c>X-Language: ru</c>.</returns>
    private HttpClient Client()
    {
        var client = _two.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");
        return client;
    }

    /// <summary>Signs in with the password and returns the pending token the API hands out.</summary>
    /// <param name="account">The two-factor account.</param>
    /// <returns>The pending token.</returns>
    private async Task<string> PendingTokenAsync(TwoFactorAccount account)
    {
        var response = await _two.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = account.Email, password = Password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("twoFactorRequired").GetBoolean().Should().BeTrue();
        return body.RootElement.GetProperty("pendingToken").GetString()!;
    }

    /// <summary>Registers an account and enables two-factor authentication on it.</summary>
    /// <returns>The account's email, id and authenticator secret.</returns>
    private async Task<TwoFactorAccount> NewTwoFactorAccountAsync()
    {
        var client = _two.CreateClient();
        var email = $"twofactor-login-{Guid.NewGuid():N}@example.com";
        var registered = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Two", lastName = "Login" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        using var created = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
        var userId = created.RootElement.GetProperty("userId").GetString()!;

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        using var session = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", session.RootElement.GetProperty("accessToken").GetString()!);
        var secret = await AuthTestSeeds.StartSetupAsync(client);
        (await client.PostAsJsonAsync("/api/me/2fa/enable", new { code = _two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return new TwoFactorAccount(email, userId, secret);
    }

    /// <summary>Asserts a 401 problem+json carrying the given code.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="code">The expected error code.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task AssertRefusalAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(code);
    }

    /// <summary>Disposes the clocked host.</summary>
    public void Dispose()
    {
        _two.Dispose();
    }
}
